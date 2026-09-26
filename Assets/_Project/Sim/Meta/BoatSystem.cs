using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.10: crowded coastal cities send a transport with 6-8 settlers to an empty island (yearly); boats sail every tick.
    // Passengers ride along (UnitStore.Boat): they neither think nor act and the water does not hurt them.
    public sealed class BoatSystem : ISimSystem
    {
        public const int YearOffset = 130;
        public const float Speed = 6f;              // tiles per second (a year is 36 s)
        public const float TransportHp = 60f;
        public const float BaseColonizeChance = 0.5f;
        public const int PassengersMin = 6, PassengersMax = 8, TargetSamples = 256;
        public const int MaxVoyageTicks = 6 * SimConst.TicksPerYear;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 50;

        public void Tick(in SimContext ctx)
        {
            var meta = ctx.Units.Meta;
            if (meta == null) return;
            if (ctx.Clock.Tick % SimConst.TicksPerYear == YearOffset && meta.Laws.IsOn(meta.LawColonization))
            {
                ref var rng = ref ctx.Rng.Get(RngStream.Meta);
                foreach (var city in meta.Civ.Cities)
                    if (!city.Dead) TryColonize(ctx, meta, city, ref rng);
            }
            for (int b = 0; b < meta.Boats.Count; b++)
                if (meta.Boats[b].State == BoatState.Sailing) Sail(ctx, meta, meta.Boats[b]);
        }

        static bool Sailing(MetaState meta, City city)
        {
            foreach (var b in meta.Boats) if (b.State == BoatState.Sailing && b.City == city.Index) return true;
            return false;
        }

        public static bool TryColonize(in SimContext ctx, MetaState meta, City city, ref SimRandom rng)
        {
            var civ = meta.Civ;
            var u = meta.Units.Store;
            if (city.Population < Settlers.MinPopulation || city.HallTier < 1 || !civ.HasComplete(city, civ.Ids.Docks)) return false;
            bool crowded = city.Population > city.HousingCapacity * 1.1f || city.Zones.Count >= CivState.MaxZonesPerCity - 10;
            if (!crowded || Sailing(meta, city)) return false;
            float chance = BaseColonizeChance;
            if (city.Kingdom >= 0) chance *= 1f + meta.TraitPct(meta.Kingdoms[city.Kingdom], "colonizeChance") / 100f;
            if (!rng.Chance(chance)) return false;
            if (!Port(civ, city, out int2 port)) return false;
            if (!FindIsland(meta, city, port, ref rng, out int2 landing, out int2 shore)) return false;
            return Launch(ctx, meta, city, port, landing, shore, ref rng) != null;
        }

        // A water tile next to one of the city's docks.
        public static bool Port(CivState civ, City city, out int2 port)
        {
            port = default;
            var map = civ.Map;
            foreach (int b in city.Buildings)
            {
                var d = civ.Buildings[b];
                if (d.Def != civ.Ids.Docks || d.State != BuildingState.Complete) continue;
                for (int y = d.Origin.y - 1; y <= d.Origin.y + d.H; y++)
                    for (int x = d.Origin.x - 1; x <= d.Origin.x + d.W; x++)
                        if (map.InBounds(x, y) && map.IsWater(x, y)) { port = new int2(x, y); return true; }
            }
            return false;
        }

        // Nearest good, unowned site on another island whose shore the port's sea reaches (Bölüm 5.10 "Sömürge kararı").
        public static bool FindIsland(MetaState meta, City city, int2 port, ref SimRandom rng, out int2 landing, out int2 shore)
        {
            landing = shore = default;
            var civ = meta.Civ;
            var map = civ.Map;
            var paths = meta.Units.Paths;
            float best = float.MaxValue;
            for (int s = 0; s < TargetSamples; s++)
            {
                int2 p = new int2(rng.Range(0, map.Width), rng.Range(0, map.Height));
                if (map.IsWater(p.x, p.y) || !map.IsWalkable(p.x, p.y) || !map.HasFlag(p.x, p.y, TileFlags.Buildable)) continue;
                if (paths.SameIsland(meta.Anchor(city), p, Mobility.Land)) continue;
                int z = map.ZoneIndexOf(p.x, p.y);
                if (map.Zones[z].OwnerCity >= 0 || SettlementSystem.CityWithin(civ, city.Species, p, CivState.NoCityRadius)) continue;
                if (CityPlanner.ZoneScore(civ, z, true) <= 0f) continue;
                float d = math.distance((float2)p, (float2)port);
                if (d >= best || !Shore(map, paths, p, port, out int2 w)) continue;
                best = d;
                landing = p;
                shore = w;
            }
            return best < float.MaxValue;
        }

        // A water tile within 6 tiles of the landing that the port's sea connects to.
        static bool Shore(WorldMap map, PathService paths, int2 land, int2 port, out int2 water)
        {
            water = default;
            for (int r = 1; r <= 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = land.x + dx, y = land.y + dy;
                        if (!map.InBounds(x, y) || !map.IsWater(x, y)) continue;
                        if (!paths.SameIsland(new int2(x, y), port, Mobility.Water)) continue;
                        water = new int2(x, y);
                        return true;
                    }
            return false;
        }

        public static Boat Launch(in SimContext ctx, MetaState meta, City city, int2 port, int2 landing, int2 shore, ref SimRandom rng)
        {
            var civ = meta.Civ;
            var u = meta.Units.Store;
            long king = city.Kingdom >= 0 ? meta.Kingdoms[city.Kingdom].KingUid : 0;
            var boat = new Boat
            {
                Index = meta.Boats.Count, Type = BoatType.Transport, State = BoatState.Sailing, Pos = (float2)port + 0.5f,
                City = city.Index, Kingdom = city.Kingdom, Hp = TransportHp, Destination = shore, Landing = landing, LaunchTick = ctx.Clock.Tick,
            };
            int want = rng.Range(PassengersMin, PassengersMax + 1);
            // young adults first (Bölüm 5.10 "genç yetişkin, ailesiz öncelikli"): residents are in slot order, so scan twice
            for (int pass = 0; pass < 2 && boat.Passengers.Count < want; pass++)
                foreach (int i in city.Residents)
                {
                    if (boat.Passengers.Count >= want) break;
                    if ((AgeStage)u.Age[i] != AgeStage.Adult || u.Boat[i] >= 0 || u.Uid[i] == city.LeaderUid || u.Uid[i] == king || u.ArmyOf[i] >= 0) continue;
                    if (pass == 0 && u.Mate[i] != EntityId.None) continue;
                    boat.Passengers.Add(i);
                    boat.PassengerUids.Add(u.Uid[i]);
                }
            if (boat.Passengers.Count < PassengersMin) return null;
            if (meta.Units.Paths.Request(port, shore, Mobility.Water, out int handle, false) != PathStatus.Ready) return null;
            boat.PathHandle = handle;
            foreach (int i in boat.Passengers)
            {
                UnitActSystem.Unload(civ, city, i);
                u.City[i] = -1;
                u.Origin[i] = city.Kingdom;
                u.Job[i] = 0;
                u.HomeBuilding[i] = -1;
                u.WorkBuilding[i] = -1;
                u.Boat[i] = boat.Index;
                u.Task[i] = (byte)UnitTask.Migrate;
                u.Action[i] = 0;
                u.Timer[i] = 0;
                u.TargetTile[i] = landing;
                meta.Units.ClearPath(i);
                u.Pos[i] = boat.Pos;
            }
            meta.Boats.Add(boat);
            return boat;
        }

        void Sail(in SimContext ctx, MetaState meta, Boat boat)
        {
            var u = meta.Units.Store;
            var paths = meta.Units.Paths;
            // passengers who died on board drop out
            for (int n = boat.Passengers.Count - 1; n >= 0; n--)
            {
                int i = boat.Passengers[n];
                if (u.State[i] == UnitStore.StateAlive && u.Uid[i] == boat.PassengerUids[n] && u.Boat[i] == boat.Index) continue;
                boat.Passengers.RemoveAt(n);
                boat.PassengerUids.RemoveAt(n);
            }
            if (boat.Hp <= 0f) { Sink(meta, boat); return; }
            if (boat.Passengers.Count == 0 || ctx.Clock.Tick - boat.LaunchTick > MaxVoyageTicks) { Land(meta, boat); return; }
            float step = Speed * SimConst.TickDt;
            while (step > 0f)
            {
                int len = paths.Length(boat.PathHandle);
                if (boat.PathStep >= len)
                {
                    if (math.distancesq(boat.Pos, (float2)boat.Destination + 0.5f) < 2.25f) { Land(meta, boat); return; }
                    // long voyages come in 512-step legs
                    paths.Release(boat.PathHandle);
                    boat.PathStep = 0;
                    if (paths.Request((int2)math.floor(boat.Pos), boat.Destination, Mobility.Water, out boat.PathHandle, false) != PathStatus.Ready)
                    {
                        boat.PathHandle = -1;
                        Land(meta, boat);
                        return;
                    }
                    continue;
                }
                float2 next = (float2)paths.Point(boat.PathHandle, boat.PathStep) + 0.5f;
                float d = math.distance(boat.Pos, next);
                if (d <= step)
                {
                    boat.Pos = next;
                    boat.PathStep++;
                    step -= d;
                }
                else
                {
                    boat.Pos += (next - boat.Pos) / d * step;
                    step = 0f;
                }
            }
            foreach (int i in boat.Passengers) u.Pos[i] = boat.Pos;
        }

        // Passengers go ashore at the nearest land and walk to the landing site, where they found the colony (Bölüm 5.2).
        static void Land(MetaState meta, Boat boat)
        {
            var u = meta.Units.Store;
            var paths = meta.Units.Paths;
            int2 at = (int2)math.floor(boat.Pos);
            int2 ashore = at;
            bool found = false;
            for (int r = 1; r <= 8 && !found; r++)
                for (int dy = -r; dy <= r && !found; dy++)
                    for (int dx = -r; dx <= r && !found; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        if (!paths.CanStand(at.x + dx, at.y + dy, Mobility.Land)) continue;
                        ashore = at + new int2(dx, dy);
                        found = true;
                    }
            foreach (int i in boat.Passengers)
            {
                u.Boat[i] = -1;
                if (found) u.Pos[i] = (float2)ashore + 0.5f;
                meta.Units.ClearPath(i);
            }
            Finish(meta, boat, BoatState.Done);
        }

        // Bölüm 5.10 "Batma": passengers fall into the water and must swim.
        public static void Sink(MetaState meta, Boat boat)
        {
            var u = meta.Units.Store;
            foreach (int i in boat.Passengers) u.Boat[i] = -1;
            Finish(meta, boat, BoatState.Sunk);
        }

        static void Finish(MetaState meta, Boat boat, BoatState state)
        {
            boat.State = state;
            boat.Passengers.Clear();
            boat.PassengerUids.Clear();
            if (boat.PathHandle >= 0) meta.Units.Paths.Release(boat.PathHandle);
            boat.PathHandle = -1;
        }
    }
}
