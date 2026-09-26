using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    public static class UnitLife
    {
        public const float BaseFertility = 0.08f;

        // Bölüm 3.7: live -> pregnant mother (birth when the status ends); egg -> an egg unit (st.in_egg) hatches in place.
        public static void Conceive(UnitWorld w, int mother, int father, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            var sp = u.SpeciesOf(mother);
            u.Mate[mother] = u.IdOf(father);
            if (sp.Reproduction == Reproduction.Live && w.StPregnant >= 0) w.AddStatus(mother, w.StPregnant);
            else if (sp.Reproduction == Reproduction.Egg)
            {
                int eggs = rng.Range(1, 4);
                for (int e = 0; e < eggs; e++)
                {
                    int baby = Birth(w, mother, father, tick, ref rng);
                    if (baby >= 0) w.AddStatus(baby, w.StInEgg);
                }
            }
        }

        public static int Birth(UnitWorld w, int mother, int father, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            if (u.Count >= UnitWorld.HardUnitCap) return -1;
            var id = u.Spawn(new UnitSpawnRequest
            {
                Species = u.Species[mother],
                Pos = u.Pos[mother] + new float2(rng.Range(-0.5f, 0.5f), rng.Range(-0.5f, 0.5f)),
                AgeYears = 0f,
                Sex = -1,
                Mother = u.IdOf(mother),
                Father = father >= 0 ? u.IdOf(father) : EntityId.None,
                Subspecies = u.Subspecies[mother],
            }, tick, ref rng);
            u.City[id.Index] = u.City[mother]; // Bölüm 6.1: babies take the mother's city
            w.Events?.Publish(new UnitBornEvent(u.Uid[id.Index], u.Species[id.Index], u.Uid[mother], father >= 0 ? u.Uid[father] : 0));
            return id.Index;
        }

        public static void Split(UnitWorld w, int parent, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            int child = Birth(w, parent, -1, tick, ref rng);
            if (child < 0) return;
            u.BirthTick[child] = u.BirthTick[parent];
            u.Age[child] = u.Age[parent];
            u.StatsDirty[child] = 1;
            float half = u.Hp[parent] * 0.5f;
            u.Hp[parent] = math.max(1f, half);
            u.Hp[child] = math.max(1f, half);
        }
    }

    // Phase 4: statuses, regeneration, physics, environment every tick; needs/aging/reproduction once a month per unit (staggered).
    public sealed class UnitLifeSystem : ISimSystem
    {
        public const float Gravity = 30f;
        public const float LavaDamagePerTick = 20f;
        public const int EnvironmentEvery = 5; // tile checks (lava, fire, drowning) staggered over 5 ticks; per-tick amounts scaled

        public SimPhase Phase => SimPhase.UnitsAct;
        public int Order => 20;

        public void Tick(in SimContext ctx)
        {
            var w = ctx.Units;
            var u = w.Store;
            var map = ctx.World;
            long tick = ctx.Clock.Tick;
            ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
            float dt = SimConst.TickDt;
            bool second = tick % SimConst.TicksPerSecond == 0;
            int count = u.Alive.Length;
            for (int k = 0; k < count; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive) continue;
                Statuses(w, i, second, tick, ref rng);
                if (u.State[i] != UnitStore.StateAlive) continue;
                Physics(w, i, dt, tick);
                if ((tick + i) % EnvironmentEvery == 0) Environment(w, ctx.Nature, i, tick);
                if (u.State[i] != UnitStore.StateAlive) continue;
                Vitals(w, i, second);
                if ((tick + i) % SimConst.TicksPerMonth == 0) Monthly(w, ctx.Nature, i, tick, ref rng);
            }
        }

        static void Statuses(UnitWorld w, int i, bool second, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            for (int s = 0; s < UnitStore.StatusSlots; s++)
            {
                int k = i * UnitStore.StatusSlots + s;
                int id = u.StatusId[k] - 1;
                if (id < 0) continue;
                var def = w.Content.StatusEffects[id];
                if (def.HpPerTick != 0f && second && (tick / SimConst.TicksPerSecond) % def.HpEveryTicks == 0)
                {
                    if (def.HpPerTick > 0f) w.Heal(i, def.HpPerTick);
                    else w.Damage(i, -def.HpPerTick, -1, id == w.StBurning ? DeathCause.Burned : id == w.StStarving ? DeathCause.Starved : DeathCause.Disease, tick);
                    if (u.State[i] != UnitStore.StateAlive) return;
                }
                if (u.StatusLeft[k] < 0 || --u.StatusLeft[k] > 0) continue;
                u.StatusId[k] = 0;
                u.StatsDirty[i] = 1;
                if (id == w.StPregnant)
                {
                    int father = u.IsAlive(u.Mate[i]) ? u.Mate[i].Index : -1;
                    UnitLife.Birth(w, i, father, tick, ref rng);
                }
            }
            if (u.StatsDirty[i] != 0) UnitStats.Recompute(u, i);
        }

        static void Physics(UnitWorld w, int i, float dt, long tick)
        {
            var u = w.Store;
            if (u.Z[i] <= 0f && u.VZ[i] <= 0f) return;
            u.VZ[i] -= Gravity * dt;
            u.Z[i] += u.VZ[i] * dt;
            if (u.Z[i] > 0f) return;
            float impact = -u.VZ[i];
            u.Z[i] = 0f;
            u.VZ[i] = 0f;
            float damage = math.max(0f, impact - 8f) * 5f;
            if (damage > 0f && !u.Has(i, UnitFlags.Fly)) w.Damage(i, damage, -1, DeathCause.Crushed, tick);
        }

        static void Environment(UnitWorld w, NatureState nature, int i, long tick)
        {
            var u = w.Store;
            var map = w.Map;
            int2 t = u.Tile(i);
            if (!map.InBounds(t.x, t.y))
            {
                u.Pos[i] = math.clamp(u.Pos[i], 0.5f, new float2(map.Width - 0.5f, map.Height - 0.5f));
                return;
            }
            int ti = map.Index(t.x, t.y);
            ushort flags = map.Flags[ti];
            bool fly = u.Has(i, UnitFlags.Fly) || u.Boat[i] >= 0; // passengers are above the water (Bölüm 5.10)

            if (!fly && map.Tables.Material[map.Ground[ti]] == TileTables.MaterialLava && !Immune(w, i, "heat"))
            {
                w.AddStatus(i, w.StBurning);
                w.Damage(i, LavaDamagePerTick * EnvironmentEvery, -1, DeathCause.Burned, tick);
                if (u.State[i] != UnitStore.StateAlive) return;
            }
            if (!fly && (flags & (ushort)TileFlags.Burning) != 0 && !Immune(w, i, "burning") && !Immune(w, i, "heat") && w.StBurning >= 0 && !u.HasStatus(i, w.StBurning))
                w.AddStatus(i, w.StBurning);
            bool inWater = !fly && (flags & (ushort)TileFlags.Water) != 0;
            if (inWater && w.StBurning >= 0 && u.HasStatus(i, w.StBurning)) u.RemoveStatus(i, w.StBurning);

            // Burning units set burnable ground alight (status note in status_effects.json).
            if (w.StBurning >= 0 && (tick + i) % 20 < EnvironmentEvery && u.HasStatus(i, w.StBurning) && nature.CanBurn(ti)) nature.Ignite(t.x, t.y, NatureState.IgniteDefault);

            // Walkers in deep water tire and drown.
            if (!fly && (flags & (ushort)TileFlags.Walkable) == 0 && !u.Has(i, UnitFlags.Swim))
            {
                u.Stamina[i] -= 1f * EnvironmentEvery;
                if (u.Stamina[i] <= 0f)
                {
                    u.Stamina[i] = 0f;
                    w.Damage(i, 2f * EnvironmentEvery, -1, DeathCause.Drowned, tick);
                }
            }
        }

        static void Vitals(UnitWorld w, int i, bool second)
        {
            var u = w.Store;
            var task = (UnitTask)u.Task[i];
            bool moving = u.PathHandle[i] >= 0 || task == UnitTask.Flee || task == UnitTask.Hunt || task == UnitTask.Attack;
            bool swimming = w.Map.InBounds(u.Tile(i).x, u.Tile(i).y) && (w.Map.Flags[w.Map.Index(u.Tile(i).x, u.Tile(i).y)] & (ushort)TileFlags.Walkable) == 0 && !u.Has(i, UnitFlags.Fly);
            float maxStamina = u.Stat(i, StatId.Stamina);
            if ((task == UnitTask.Flee || task == UnitTask.Hunt || task == UnitTask.Attack) && moving || swimming) u.Stamina[i] = math.max(0f, u.Stamina[i] - 0.2f);
            else u.Stamina[i] = math.min(maxStamina, u.Stamina[i] + (task == UnitTask.Rest || task == UnitTask.Sleep ? 0.5f : 0.3f));

            float maxMana = math.max(u.Stat(i, StatId.Mana), u.SpeciesOf(i).SpellIds.Length > 0 ? 100f : 0f);
            if (maxMana > 0f) u.Mana[i] = math.min(maxMana, u.Mana[i] + u.Stat(i, StatId.ManaRegen));

            if (second)
            {
                float regen = u.Stat(i, StatId.Regen);
                if (regen > 0f) w.Heal(i, regen);
            }
        }

        static void Monthly(UnitWorld w, NatureState nature, int i, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            var sp = u.SpeciesOf(i);
            var laws = nature.Laws;

            // needs; eggs live on the yolk (DECISIONS #50), passengers on ship's stores (DECISIONS #64)
            bool inEgg = (w.StInEgg >= 0 && u.HasStatus(i, w.StInEgg)) || u.Boat[i] >= 0;
            if (!u.Has(i, UnitFlags.NoNeeds) && !inEgg)
            {
                if (laws.IsOn(w.LawHunger))
                {
                    int drop = (int)math.round(8f * u.Stat(i, StatId.HungerRate));
                    // nursing: a baby next to its fed mother does not go hungry; the mother eats for two (DECISIONS #53)
                    var mother = u.Mother[i];
                    if (u.Age[i] == (byte)AgeStage.Baby && u.IsAlive(mother) && u.Saturation[mother.Index] > 20
                        && math.distancesq(u.Pos[i], u.Pos[mother.Index]) <= NurseRadius * NurseRadius)
                    {
                        u.Saturation[mother.Index] = (byte)math.max(0, u.Saturation[mother.Index] - drop / 2);
                        drop = 0;
                    }
                    int sat = u.Saturation[i] - drop;
                    u.Saturation[i] = (byte)math.clamp(sat, 0, 100);
                    if (u.Saturation[i] == 0)
                    {
                        if (w.StStarving >= 0 && !u.HasStatus(i, w.StStarving)) u.AddStatus(i, w.StStarving, -1);
                        if (++u.StarveMonths[i] >= 3)
                        {
                            u.Kill(i, DeathCause.Starved);
                            return;
                        }
                    }
                    else if (u.StarveMonths[i] > 0)
                    {
                        u.StarveMonths[i] = 0;
                        if (w.StStarving >= 0) u.RemoveStatus(i, w.StStarving);
                    }
                }
                int energy = u.Energy[i] - (int)math.round(12f * u.Stat(i, StatId.SleepNeed));
                u.Energy[i] = (byte)math.clamp(energy, 0, 100);
            }

            // happiness drift from statuses; city residents get theirs from the civ monthly pass (Bölüm 5.8)
            if (u.City[i] < 0) Mood(w, i);

            // temperature damage (Bölüm 2.5 thresholds)
            int2 t = u.Tile(i);
            // city folk with a home are sheltered from the weather (DECISIONS #59)
            if (w.Map.InBounds(t.x, t.y) && u.HomeBuilding[i] < 0)
            {
                short temp = w.Map.Zones[w.Map.ZoneIndexOf(t.x, t.y)].TemperatureC;
                // damage grows with the distance past the threshold: 0.5 hp per degree per month (DECISIONS #49)
                if (temp > 35 && !Immune(w, i, "heat")) w.Damage(i, (temp - 35) * 0.5f, -1, DeathCause.Burned, tick);
                else if (temp < -10 && !Immune(w, i, "cold")) w.Damage(i, (-10 - temp) * 0.5f, -1, DeathCause.Frozen, tick);
                if (u.State[i] != UnitStore.StateAlive) return;
            }

            // natural healing when fed
            if (u.Saturation[i] > 30 || u.Has(i, UnitFlags.NoNeeds) || inEgg) w.Heal(i, u.Stat(i, StatId.Hp) * NaturalHealPerMonth);

            // aging
            float age = u.AgeYears(i, tick);
            var stage = UnitStats.StageFor(sp, age);
            if ((byte)stage != u.Age[i])
            {
                u.Age[i] = (byte)stage;
                u.StatsDirty[i] = 1;
            }
            float lifespan = u.Stat(i, StatId.Lifespan);
            bool yearly = ((tick / SimConst.TicksPerMonth) + i) % SimConst.MonthsPerYear == 0;
            if (yearly && lifespan > 0f && !u.Has(i, UnitFlags.NoAging) && laws.IsOn(w.LawAging) && age > lifespan)
            {
                float p = math.min(0.9f, 0.1f + 0.15f * (age / lifespan - 1f) * 10f);
                if (rng.Chance(p))
                {
                    u.Kill(i, DeathCause.OldAge);
                    return;
                }
            }

            // reproduction roll
            if (stage != AgeStage.Adult || !laws.IsOn(w.LawReproduction) || u.Saturation[i] <= 60 || u.Happiness[i] <= -20) return;
            if ((w.StPregnant >= 0 && u.HasStatus(i, w.StPregnant)) || u.Has(i, UnitFlags.Immobile)) return;
            var rep = sp.Reproduction;
            if (rep != Reproduction.Live && rep != Reproduction.Egg && rep != Reproduction.Split) return;
            // local carrying capacity (DECISIONS #48); city folk are limited by their homes instead (DECISIONS #55)
            if (u.City[i] < 0 && KinNearby(w, i) >= CrowdLimit) return;
            if (u.City[i] >= 0 && w.Civ != null && CityCrowded(w.Civ.Cities[u.City[i]])) return;
            float chance = UnitLife.BaseFertility * u.Stat(i, StatId.Fertility) * nature.Mods.FertilityMul * CapFactor(w, nature, u.Species[i]);
            if (!rng.Chance(chance)) return;
            if (rep == Reproduction.Split) UnitLife.Split(w, i, tick, ref rng);
            else u.Wants[i] |= 1;
        }

        static void Mood(UnitWorld w, int i)
        {
            var u = w.Store;
            float mood = 0f;
            for (int s = 0; s < UnitStore.StatusSlots; s++)
            {
                int id = u.StatusId[i * UnitStore.StatusSlots + s] - 1;
                if (id >= 0) mood += w.Content.StatusEffects[id].HappinessPerMonth;
            }
            u.Happiness[i] = (sbyte)math.clamp(u.Happiness[i] + (int)mood - math.sign(u.Happiness[i]), -100, 100);
        }

        // Bölüm 3.7 population cap: near the cap (or over the species share) fertility x0.2; the hard cap stops births.
        static float CapFactor(UnitWorld w, NatureState nature, int species)
        {
            var laws = nature.Laws;
            float cap = laws.IsOn(w.LawPopulationCap) ? laws.Value(w.LawPopulationCap) : UnitWorld.HardUnitCap;
            int total = w.Store.Count;
            if (total >= cap || total >= UnitWorld.HardUnitCap) return 0f;
            float speciesCap = math.max(200f, cap * 0.25f);
            if (w.SpeciesCount[species] >= speciesCap || total >= cap * 0.9f) return 0.2f;
            return 1f;
        }

        public const int CrowdLimit = 12;

        // A city stops growing once people clearly outnumber its beds (houses come first, then children).
        static bool CityCrowded(City city) => city.Population > city.HousingCapacity + 6;
        public const float NurseRadius = 8f;
        public const float NaturalHealPerMonth = 0.05f;

        // Same-species units in the 3x3 zone cells around the unit (24x24 tiles).
        static int KinNearby(UnitWorld w, int i)
        {
            var u = w.Store;
            var index = w.Index;
            int cell = index.CellOf(u.Pos[i]);
            int cx = cell % index.CellsX, cy = cell / index.CellsX, count = 0;
            for (int y = math.max(0, cy - 1); y <= math.min(index.CellsY - 1, cy + 1); y++)
                for (int x = math.max(0, cx - 1); x <= math.min(index.CellsX - 1, cx + 1); x++)
                {
                    int c = y * index.CellsX + x;
                    for (int k = index.CellStart[c]; k < index.CellStart[c + 1]; k++)
                        if (u.Species[index.Sorted[k]] == u.Species[i]) count++;
                }
            return count;
        }

        public static bool Immune(UnitWorld w, int i, string what)
        {
            var u = w.Store;
            var sp = u.SpeciesOf(i);
            if (System.Array.IndexOf(sp.Immunes, what) >= 0) return true;
            var traits = u.Traits[i];
            if (traits.IsEmpty) return false;
            for (int t = 0; t < w.Content.UnitTraits.Count; t++)
                if (traits.Has(t) && w.Content.UnitTraits[t].Immune(what)) return true;
            return false;
        }
    }

    // Bölüm 3.9: arcing projectiles; the target point is fixed at launch (no lead), so moving targets can dodge.
    public sealed class ProjectileSystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.UnitsAct;
        public int Order => 10;

        public void Tick(in SimContext ctx)
        {
            var w = ctx.Units;
            var list = w.Projectiles;
            if (list.Length == 0) return;
            var u = w.Store;
            long tick = ctx.Clock.Tick;
            for (int k = list.Length - 1; k >= 0; k--)
            {
                var p = list[k];
                p.T += SimConst.TickDt / p.Duration;
                if (p.T < 1f)
                {
                    p.Pos = math.lerp(p.From, p.To, p.T);
                    p.Z = math.sin(math.PI * p.T) * math.distance(p.From, p.To) * 0.25f;
                    list[k] = p;
                    continue;
                }
                list.RemoveAt(k);
                int owner = u.State[p.Owner] == UnitStore.StateAlive && u.Uid[p.Owner] == p.OwnerUid ? p.Owner : -1;
                float radius = math.max(0.6f, p.Area);
                w.Index.CellRange(p.To, radius, out int x0, out int y0, out int x1, out int y1);
                for (int cy = y0; cy <= y1; cy++)
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        int cell = cy * w.Index.CellsX + cx;
                        for (int c = w.Index.CellStart[cell]; c < w.Index.CellStart[cell + 1]; c++)
                        {
                            int o = w.Index.Sorted[c];
                            if (o == p.Owner || u.State[o] != UnitStore.StateAlive) continue;
                            if (owner >= 0 && u.Species[o] == u.Species[owner]) continue;
                            if (math.distance(u.Pos[o], p.To) > radius) continue;
                            w.Damage(o, p.Damage, owner, DeathCause.Killed, tick);
                            if (p.Area <= 0f) goto done; // single target
                        }
                    }
                done:
                if (p.Ignite != 0)
                {
                    int2 t = (int2)math.floor(p.To);
                    if (ctx.World.InBounds(t.x, t.y)) ctx.Nature.Ignite(t.x, t.y, 150);
                }
            }
        }
    }

    // EventsFlush phase, before event dispatch: frees dead slots; corpses turn into bone piles after two years.
    public sealed class UnitCleanupSystem : ISimSystem
    {
        public const int CorpseYears = 2;

        public SimPhase Phase => SimPhase.EventsFlush;
        public int Order => -10;

        public void Tick(in SimContext ctx)
        {
            var w = ctx.Units;
            long tick = ctx.Clock.Tick;
            // release the paths of the dying before their slots are reused
            var u = w.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] == UnitStore.StateDying) w.ClearPath(i);
            }
            u.FlushDeaths(ctx.Events, tick, ctx.World.Width);

            if (tick % SimConst.TicksPerMonth != 0) return;
            var corpses = u.Corpses;
            var map = ctx.World;
            for (int k = corpses.Length - 1; k >= 0; k--)
            {
                if (tick - corpses[k].Tick < CorpseYears * SimConst.TicksPerYear) continue;
                int tile = corpses[k].Tile;
                int x = tile % map.Width, y = tile / map.Width;
                if (w.BonesFeature != 0 && map.Feature[tile] == 0) map.SetFeature(x, y, w.BonesFeature, 2, ChangeSource.Unit);
                corpses.RemoveAt(k);
            }
        }
    }

    // Initial animals (worldgen step 10) and the yearly natural spawn (law.animal_spawn).
    public sealed class AnimalSpawnSystem : ISimSystem
    {
        public const float InitialGroupChance = 0.05f;
        public const int NaturalZonesPerYear = 12, NaturalSpeciesLimit = 30;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 30;

        public void Tick(in SimContext ctx)
        {
            if (!ctx.Clock.IsYearStart || ctx.Clock.Tick == 0) return;
            var w = ctx.Units;
            if (!ctx.Nature.Laws.IsOn(w.LawAnimalSpawn)) return;
            ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
            var map = ctx.World;
            for (int n = 0; n < NaturalZonesPerYear; n++)
            {
                int z = rng.Range(0, map.Zones.Length);
                byte b = map.Zones[z].DominantBiome;
                if (b == 0 || map.Zones[z].LandTiles < 32) continue;
                var animals = ctx.Content.Biomes[b - 1].AnimalSpecies;
                if (animals.Length == 0) continue;
                int sp = animals[rng.Range(0, animals.Length)];
                if (w.SpeciesCount[sp] >= NaturalSpeciesLimit) continue;
                SpawnGroup(w, map, z, sp, 2, ctx.Clock.Tick, ref rng);
            }
        }

        public static int SpawnInitial(UnitWorld w, WorldMap map, ContentDB content, long tick, ref SimRandom rng)
        {
            int spawned = 0;
            for (int z = 0; z < map.Zones.Length; z++)
            {
                if (!rng.Chance(InitialGroupChance)) continue;
                int zx = z % map.ZonesX, zy = z / map.ZonesX;
                int x = (zx << WorldMap.ZoneShift) + 4, y = (zy << WorldMap.ZoneShift) + 4;
                byte b = map.Biome[map.Index(x, y)];
                if (b == 0) continue;
                var animals = content.Biomes[b - 1].AnimalSpecies;
                if (animals.Length == 0) continue;
                spawned += SpawnGroup(w, map, z, animals[rng.Range(0, animals.Length)], rng.Range(2, 5), tick, ref rng);
            }
            return spawned;
        }

        static int SpawnGroup(UnitWorld w, WorldMap map, int zone, int species, int count, long tick, ref SimRandom rng)
        {
            var sp = w.Content.Species[species];
            int zx = zone % map.ZonesX, zy = zone / map.ZonesX;
            int made = 0;
            for (int n = 0; n < count && w.Store.Count < UnitWorld.HardUnitCap; n++)
            {
                for (int tries = 0; tries < 8; tries++)
                {
                    int x = (zx << WorldMap.ZoneShift) + rng.Range(0, WorldMap.ZoneSize);
                    int y = (zy << WorldMap.ZoneShift) + rng.Range(0, WorldMap.ZoneSize);
                    var mob = sp.Habitat == Habitat.Air ? Mobility.Fly : sp.Habitat == Habitat.Water ? Mobility.Water : Mobility.Land;
                    if (!w.Paths.CanStand(x, y, mob) || (mob == Mobility.Fly && map.IsWater(x, y))) continue;
                    float lifespan = sp.BaseStats[(int)StatId.Lifespan];
                    w.Store.Spawn(new UnitSpawnRequest
                    {
                        Species = species, Pos = new float2(x + 0.5f, y + 0.5f),
                        AgeYears = lifespan > 0f ? rng.Range(0.2f, 0.6f) * lifespan : 5f,
                        Sex = n % 2, Mother = EntityId.None, Father = EntityId.None, Subspecies = -1,
                    }, tick, ref rng);
                    made++;
                    break;
                }
            }
            return made;
        }
    }
}
