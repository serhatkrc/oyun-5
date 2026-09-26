using System;
using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.2: city-less sapient adults that gather (>= 6 of a kind within 20 tiles, no city of theirs within 30) found a village.
    public sealed class SettlementSystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.Civ;
        public int Order => 10;

        readonly List<int> _group = new List<int>();

        public void Tick(in SimContext ctx)
        {
            if (ctx.Clock.Tick % SimConst.TicksPerMonth != SimConst.TicksPerMonth / 2) return;
            var civ = ctx.Units.Civ;
            if (civ == null) return;
            var u = ctx.Units.Store;
            ref var rng = ref ctx.Rng.Get(RngStream.Civ);
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (!CanFound(ctx.Units, i)) continue;
                if ((UnitTask)u.Task[i] == UnitTask.Migrate) continue; // settlers found where they are headed
                Gather(ctx.Units, i, _group);
                if (_group.Count < CivState.FoundingGroup) continue;
                if (CityWithin(civ, u.Species[i], u.Tile(i), CivState.NoCityRadius)) continue;
                Found(ctx, civ, _group, ref rng);
            }
        }

        public static bool CanFound(UnitWorld w, int i)
        {
            var u = w.Store;
            if (u.State[i] != UnitStore.StateAlive || u.City[i] >= 0) return false;
            if (!u.SpeciesOf(i).IsCiv) return false;
            var stage = (AgeStage)u.Age[i];
            return stage == AgeStage.Adult || stage == AgeStage.Elder;
        }

        // Same-species city-less units around `i` (any age), `i` first.
        public static void Gather(UnitWorld w, int i, List<int> group)
        {
            group.Clear();
            group.Add(i);
            var u = w.Store;
            var index = w.Index;
            index.CellRange(u.Pos[i], CivState.FoundingRadius, out int x0, out int y0, out int x1, out int y1);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cy * index.CellsX + cx;
                    for (int c = index.CellStart[cell]; c < index.CellStart[cell + 1]; c++)
                    {
                        int o = index.Sorted[c];
                        if (o == i || u.State[o] != UnitStore.StateAlive || u.City[o] >= 0 || u.Species[o] != u.Species[i]) continue;
                        if (math.distancesq(u.Pos[o], u.Pos[i]) > CivState.FoundingRadius * CivState.FoundingRadius) continue;
                        group.Add(o);
                    }
                }
        }

        public static bool CityWithin(CivState civ, int species, int2 at, int radius)
        {
            var map = civ.Map;
            int zr = radius / WorldMap.ZoneSize + 1;
            int zx0 = at.x >> WorldMap.ZoneShift, zy0 = at.y >> WorldMap.ZoneShift;
            for (int zy = zy0 - zr; zy <= zy0 + zr; zy++)
                for (int zx = zx0 - zr; zx <= zx0 + zr; zx++)
                {
                    if (zx < 0 || zy < 0 || zx >= map.ZonesX || zy >= map.ZonesY) continue;
                    int owner = map.Zones[zy * map.ZonesX + zx].OwnerCity;
                    if (owner < 0) continue;
                    var c = civ.Cities[owner];
                    if (c.Dead || c.Species != species) continue;
                    float2 zc = new float2(zx * 8 + 4, zy * 8 + 4);
                    if (math.distance(zc, at) <= radius) return true;
                }
            return false;
        }

        public static City Found(in SimContext ctx, CivState civ, List<int> group, ref SimRandom rng)
        {
            var u = ctx.Units.Store;
            float2 sum = 0f;
            foreach (int g in group) sum += u.Pos[g];
            int2 center = (int2)math.floor(sum / group.Count);
            if (!CityPlanner.BestFoundingZone(civ, center, out int zone, out int2 origin)) return null;

            var city = civ.CreateCity(u.Species[group[0]], origin, ctx.Clock.Tick, ctx.Clock.Year, ref rng);
            civ.ClaimZone(city, zone);
            CityPlanner.ClaimAround(civ, city, zone, 9);
            int bonfire = civ.Place(civ.Ids.Bonfire, origin, city, ctx.Clock.Tick, complete: true);
            civ.Complete(bonfire, ctx.Clock.Tick, ctx.Events);
            int leader = -1;
            float best = float.MinValue;
            foreach (int g in group)
            {
                u.City[g] = city.Index;
                if ((AgeStage)u.Age[g] != AgeStage.Adult && (AgeStage)u.Age[g] != AgeStage.Elder) continue;
                float score = u.Stat(g, StatId.Steward) + u.Stat(g, StatId.Diplo);
                if (score > best) { best = score; leader = g; }
            }
            if (leader >= 0)
            {
                city.LeaderUid = u.Uid[leader];
                if (civ.Ids.JobLeader >= 0) u.Job[leader] = (byte)(civ.Ids.JobLeader + 1);
            }
            CivMonthlySystem.UpdateCity(ctx, civ, city, ref rng);
            ctx.Events.Publish(new CityFoundedEvent(city.Index, leader >= 0 ? u.Uid[leader] : 0));
            return city;
        }
    }

    // Bölüm 5.3-5.8, monthly for every city (month start): residents, homes, food, spoilage, famine, taxes, jobs, crops, happiness.
    public sealed class CivMonthlySystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.Civ;
        public int Order => 20;

        public void Tick(in SimContext ctx)
        {
            if (ctx.Clock.Tick % SimConst.TicksPerMonth != 0) return;
            var civ = ctx.Units.Civ;
            if (civ == null) return;
            ref var rng = ref ctx.Rng.Get(RngStream.Civ);
            RebuildResidents(ctx.Units, civ);
            for (int c = 0; c < civ.Cities.Count; c++)
            {
                var city = civ.Cities[c];
                if (city.Dead) continue;
                UpdateCity(ctx, civ, city, ref rng);
            }
        }

        // After a load: the monthly caches (residents, population, capacities, food) are derived, not saved.
        public static void RefreshCaches(UnitWorld w, CivState civ)
        {
            RebuildResidents(w, civ);
            foreach (var city in civ.Cities)
            {
                if (city.Dead) continue;
                city.Population = city.Residents.Count;
                Capacities(civ, city);
                city.FoodTotal = FoodNutrition(civ, city);
            }
        }

        static void RebuildResidents(UnitWorld w, CivState civ)
        {
            foreach (var city in civ.Cities) city.Residents.Clear();
            var u = w.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                int c = u.City[i];
                if (c < 0 || u.State[i] != UnitStore.StateAlive) continue;
                if (civ.Cities[c].Dead) { u.City[i] = -1; u.Job[i] = 0; continue; }
                civ.Cities[c].Residents.Add(i);
            }
        }

        public static void UpdateCity(in SimContext ctx, CivState civ, City city, ref SimRandom rng)
        {
            long tick = ctx.Clock.Tick;
            city.Population = city.Residents.Count;
            if (city.Population == 0)
            {
                if (city.EmptySinceTick < 0) city.EmptySinceTick = tick;
                else if (tick - city.EmptySinceTick >= CivState.AbandonYears * SimConst.TicksPerYear) civ.KillCity(city, tick, ctx.Events);
                return;
            }
            city.EmptySinceTick = -1;

            Capacities(civ, city);
            Homes(ctx.Units, civ, city);
            Feed(ctx, civ, city);
            Spoil(civ, city);
            Taxes(ctx.Units, civ, city);
            JobAssigner.Assign(ctx.Units, civ, city);
            Crops(civ, city);
            Happiness(ctx.Units, civ, city);
            ReturnDeadOwnersItems(ctx.Units, civ, city);
            Crafting.HandOut(ctx.Units, civ, city);
            Leader(ctx.Units, civ, city);
        }

        public static void Capacities(CivState civ, City city)
        {
            int housing = 0, storages = 0, constructions = 0;
            foreach (int b in city.Buildings)
            {
                var data = civ.Buildings[b];
                var def = civ.Content.Buildings[data.Def];
                if (data.State == BuildingState.Construction) { constructions++; continue; }
                if (data.State != BuildingState.Complete) continue;
                if (def.IsHouse) housing += def.Capacity;
                if (data.Def == civ.Ids.Storage) storages++;
            }
            city.HousingCapacity = housing;
            city.ActiveConstructions = constructions;
            city.StorageCapacity = CivState.BaseStorage + storages * CivState.StoragePerBuilding + city.HallTier * CivState.StoragePerTier;
        }

        // Houses: recount occupants, then give the homeless a free bed.
        static void Homes(UnitWorld w, CivState civ, City city)
        {
            var u = w.Store;
            foreach (int b in city.Buildings)
            {
                var data = civ.Buildings[b];
                if (data.Residents == 0) continue;
                data.Residents = 0;
                civ.Buildings[b] = data;
            }
            foreach (int i in city.Residents)
            {
                int h = u.HomeBuilding[i];
                if (h < 0) continue;
                var data = civ.Buildings[h];
                if (data.State != BuildingState.Complete || data.City != city.Index) { u.HomeBuilding[i] = -1; continue; }
                data.Residents++;
                civ.Buildings[h] = data;
            }
            foreach (int i in city.Residents)
            {
                if (u.HomeBuilding[i] >= 0) continue;
                int home = -1;
                foreach (int b in city.Buildings)
                {
                    var data = civ.Buildings[b];
                    var def = civ.Content.Buildings[data.Def];
                    if (data.State != BuildingState.Complete || !def.IsHouse || data.Residents >= def.Capacity) continue;
                    home = b;
                    break;
                }
                if (home < 0)
                {
                    Happy.Add(u, i, civ.Ids.HapHomeless, civ.Content);
                    continue;
                }
                var h = civ.Buildings[home];
                h.Residents++;
                civ.Buildings[home] = h;
                u.HomeBuilding[i] = home;
                u.Home[i] = h.Door;
                Happy.Add(u, i, civ.Ids.HapNewHome, civ.Content);
            }
        }

        // Bölüm 5.5: every resident eats one nutrition unit a month, best food first; fed residents are full.
        static void Feed(in SimContext ctx, CivState civ, City city)
        {
            var u = ctx.Units.Store;
            var db = civ.Content;
            var diet = db.Species[city.Species].Diet;
            // only the hungry eat from the stock; those who ate in the wild keep it for later (DECISIONS #55)
            int hungry = 0;
            foreach (int i in city.Residents) if (u.Saturation[i] < FedSaturation) hungry++;
            int need = hungry, fed = 0;
            while (need > 0)
            {
                int best = -1;
                for (int r = 0; r < db.Resources.Count; r++)
                {
                    var res = db.Resources[r];
                    if (city.Stock[r] <= 0 || !res.IsFood || !Edible(res, diet)) continue;
                    if (best < 0 || res.Nutrition > db.Resources[best].Nutrition) best = r;
                }
                if (best < 0) break;
                city.Stock[best]--;
                int n = db.Resources[best].Nutrition;
                fed += math.min(need, n);
                need -= n;
            }
            city.FoodTotal = FoodNutrition(civ, city);
            int served = 0;
            for (int k = 0; k < city.Residents.Count; k++)
            {
                int i = city.Residents[k];
                if (u.Saturation[i] >= FedSaturation) continue;
                if (served++ < fed)
                {
                    u.Saturation[i] = (byte)math.max(u.Saturation[i], FedSaturation);
                    if (u.StarveMonths[i] > 0 && ctx.Units.StStarving >= 0) u.RemoveStatus(i, ctx.Units.StStarving);
                    u.StarveMonths[i] = 0;
                }
                else Happy.Add(u, i, civ.Ids.HapHungry, db);
            }
            if (fed < hungry)
            {
                city.FamineMonths++;
                if (city.FamineMonths % 3 == 0) ctx.Events.Publish(new FamineEvent(city.Index));
            }
            else city.FamineMonths = 0;
        }

        public const int FedSaturation = 85;

        public static bool Edible(ResourceDef res, Diet diet)
        {
            switch (diet)
            {
                case Diet.Herb: return !res.IsMeat;
                case Diet.Carn: return res.IsMeat;
                default: return true;
            }
        }

        public static int FoodNutrition(CivState civ, City city)
        {
            int n = 0;
            var db = civ.Content;
            for (int r = 0; r < db.Resources.Count; r++)
                if (db.Resources[r].IsFood) n += city.Stock[r] * db.Resources[r].Nutrition;
            return n;
        }

        // 4% of each food a month (2% with a granary); fractions round down, so small stocks keep.
        static void Spoil(CivState civ, City city)
        {
            int pct = civ.HasComplete(city, civ.Ids.Granary) ? 2 : 4;
            var db = civ.Content;
            for (int r = 0; r < db.Resources.Count; r++)
                if (db.Resources[r].IsFood && city.Stock[r] > 0) city.Stock[r] -= city.Stock[r] * pct / 100;
        }

        // gold = residents x 0.1 x (1 + steward/20); integer maths, remainder carried in the city's gold via rounding down.
        static void Taxes(UnitWorld w, CivState civ, City city)
        {
            float steward = 0f;
            int leader = LeaderIndex(w, city);
            if (leader >= 0) steward = w.Store.Stat(leader, StatId.Steward);
            city.Gold += (int)(city.Population * (20f + steward) / 200f);
        }

        // Wheat on the city's fields ripens one stage a month (DECISIONS #57).
        static void Crops(CivState civ, City city)
        {
            ushort crop = civ.Ids.WheatCrop;
            if (crop == 0) return;
            var map = civ.Map;
            foreach (int z in city.Zones)
            {
                int zx = (z % map.ZonesX) << WorldMap.ZoneShift, zy = (z / map.ZonesX) << WorldMap.ZoneShift;
                for (int y = zy; y < zy + WorldMap.ZoneSize; y++)
                    for (int x = zx; x < zx + WorldMap.ZoneSize; x++)
                    {
                        int i = map.Index(x, y);
                        if (map.Feature[i] != crop) continue;
                        int stage = map.GetFeatureStage(i);
                        if (stage < 3) map.SetFeatureStage(x, y, stage + 1);
                    }
            }
        }

        // Bölüm 5.8: happiness = active events + statuses + home comfort; the city is the average.
        static void Happiness(UnitWorld w, CivState civ, City city)
        {
            var u = w.Store;
            var db = civ.Content;
            int sum = 0;
            foreach (int i in city.Residents)
            {
                int value = Happy.Tick(u, i, db);
                for (int s = 0; s < UnitStore.StatusSlots; s++)
                {
                    int id = u.StatusId[i * UnitStore.StatusSlots + s] - 1;
                    if (id >= 0) value += (int)db.StatusEffects[id].HappinessPerMonth;
                }
                int home = u.HomeBuilding[i];
                if (home >= 0)
                {
                    int def = civ.Buildings[home].Def;
                    if (def == civ.Ids.House) value += 1;
                    else if (def == civ.Ids.Manor) value += 2;
                }
                if (civ.HasComplete(city, civ.Ids.Inn)) value += 2;
                u.Happiness[i] = (sbyte)math.clamp(value, -100, 100);
                sum += u.Happiness[i];
            }
            city.Happiness = (short)(city.Population > 0 ? sum / city.Population : 0);
        }

        static void ReturnDeadOwnersItems(UnitWorld w, CivState civ, City city)
        {
            var u = w.Store;
            for (int it = 0; it < civ.Items.Length; it++)
            {
                var item = civ.Items[it];
                if (item.Alive == 0 || item.Owner < 0) continue;
                if (u.State[item.Owner] == UnitStore.StateAlive && u.Equip[item.Owner * UnitStore.EquipSlots + (int)civ.Content.EquipmentTypes[item.Type].SlotCode] == it) continue;
                if (item.City != city.Index) continue;
                item.Owner = -1;
                civ.Items[it] = item;
            }
        }

        // The leader died or left: highest steward + diplo adult takes over.
        static void Leader(UnitWorld w, CivState civ, City city)
        {
            if (LeaderIndex(w, city) >= 0) return;
            var u = w.Store;
            int best = -1;
            float bestScore = float.MinValue;
            foreach (int i in city.Residents)
            {
                var stage = (AgeStage)u.Age[i];
                if (stage != AgeStage.Adult && stage != AgeStage.Elder) continue;
                float s = u.Stat(i, StatId.Steward) + u.Stat(i, StatId.Diplo);
                if (s > bestScore) { bestScore = s; best = i; }
            }
            if (best < 0) return;
            city.LeaderUid = u.Uid[best];
            if (civ.Ids.JobLeader >= 0) u.Job[best] = (byte)(civ.Ids.JobLeader + 1);
        }

        public static int LeaderIndex(UnitWorld w, City city)
        {
            var u = w.Store;
            foreach (int i in city.Residents)
                if (u.Uid[i] == city.LeaderUid && u.State[i] == UnitStore.StateAlive) return i;
            return -1;
        }
    }

    // Happiness event ring (Bölüm 5.8): 8 slots per unit, events last 6 months.
    public static class Happy
    {
        public const int Months = 6;

        public static void Add(UnitStore u, int i, int ev, ContentDB db)
        {
            if (ev < 0) return;
            int baseSlot = i * UnitStore.HapSlots, slot = -1, oldest = int.MaxValue;
            for (int s = 0; s < UnitStore.HapSlots; s++)
            {
                int k = baseSlot + s;
                if (u.HapEvent[k] == ev + 1) { u.HapLeft[k] = Months; return; } // same event refreshes
                if (u.HapEvent[k] == 0) { slot = k; break; }
                if (u.HapLeft[k] < oldest) { oldest = u.HapLeft[k]; slot = k; }
            }
            u.HapEvent[slot] = (ushort)(ev + 1);
            u.HapLeft[slot] = Months;
        }

        // Sum of active events; ages them by a month.
        public static int Tick(UnitStore u, int i, ContentDB db)
        {
            int sum = 0;
            for (int s = 0; s < UnitStore.HapSlots; s++)
            {
                int k = i * UnitStore.HapSlots + s;
                int ev = u.HapEvent[k] - 1;
                if (ev < 0) continue;
                sum += db.HappinessEvents[ev].Value;
                if (--u.HapLeft[k] == 0) u.HapEvent[k] = 0;
            }
            return sum;
        }
    }

    // Bölüm 5.6 JobAssigner.
    public static class JobAssigner
    {
        static readonly List<int> Idle = new List<int>();

        public static void Assign(UnitWorld w, CivState civ, City city)
        {
            var u = w.Store;
            var ids = civ.Ids;
            var db = civ.Content;
            if (Desired.Length < db.Jobs.Count) Desired = new int[db.Jobs.Count];
            Array.Clear(city.JobQuota, 0, city.JobQuota.Length);
            Array.Clear(city.JobCount, 0, city.JobCount.Length);
            int pop = city.Population;
            int food = city.FoodTotal;

            void Q(int job, int n) { if (job >= 0) city.JobQuota[job] += math.max(0, n); }
            int Slots(int building) => building < 0 ? 0 : civ.CountBuildings(city, building, false) * db.Buildings[building].JobSlots;

            Q(ids.JobBuilder, math.max(1, pop / 8) + city.ActiveConstructions);
            Q(ids.JobFarmer, Slots(ids.FarmShed));
            // doc: pop/10 gatherers and pop/10 lumberjacks; a young village needs more hands on food and wood (DECISIONS #55)
            Q(ids.JobGatherer, food < pop * 24 ? math.max(1, pop / (city.HallTier == 0 ? 4 : 10)) : 0);
            bool needStone = city.StockOf(ids.Stone) < UnitActSystem.QuarryBelow && !civ.HasComplete(city, ids.Mine);
            Q(ids.JobLumberjack, city.StockOf(ids.Wood) < CivState.WoodTarget || needStone ? math.max(2, pop / (city.HallTier == 0 ? 4 : 10)) + Slots(ids.LumberCamp) : 0);
            Q(ids.JobMiner, Slots(ids.Mine));
            Q(ids.JobFisher, Slots(ids.FishingHut));
            Q(ids.JobHunter, pop >= 6 ? math.max(1, pop / 15) : 0);
            Q(ids.JobHerder, Slots(ids.Pasture));
            Q(ids.JobSmith, Slots(ids.Smithy));
            Q(ids.JobBaker, Slots(ids.Bakery));
            if (civ.HasComplete(city, ids.Barracks)) Q(ids.JobWarrior, (int)(pop * 0.05f));
            Q(ids.JobGuard, Slots(ids.Watchtower));

            // Desired head count per job: tiers hand out the available adults so a small village still gets food, one builder,
            // one lumberjack and one hunter before anybody's second helper (Bölüm 5.6 step 2 priorities; DECISIONS #55).
            int adults = 0;
            foreach (int i in city.Residents) if (CanWork(u, i) && u.Job[i] - 1 != ids.JobLeader) adults++;
            Array.Clear(Desired, 0, Desired.Length);
            int left = adults;
            void Want(int job, int cap)
            {
                if (job < 0 || left <= 0) return;
                int add = math.min(left, math.min(cap, city.JobQuota[job] - Desired[job]));
                if (add <= 0) return;
                Desired[job] += add;
                left -= add;
            }
            Want(ids.JobFarmer, math.max(1, adults / 2));
            Want(ids.JobGatherer, 1);
            Want(ids.JobBuilder, 1 + city.ActiveConstructions / 2);
            Want(ids.JobLumberjack, 1);
            Want(ids.JobHunter, 1);
            Want(ids.JobFarmer, int.MaxValue);
            Want(ids.JobFisher, int.MaxValue);
            Want(ids.JobGatherer, int.MaxValue);
            Want(ids.JobBuilder, int.MaxValue);
            Want(ids.JobHerder, int.MaxValue);
            Want(ids.JobBaker, int.MaxValue);
            Want(ids.JobLumberjack, int.MaxValue);
            Want(ids.JobHunter, int.MaxValue);
            Want(ids.JobWarrior, int.MaxValue);
            Want(ids.JobGuard, int.MaxValue);
            Want(ids.JobMiner, int.MaxValue);
            Want(ids.JobSmith, int.MaxValue);

            // keep current workers up to the wanted count; release the rest
            foreach (int i in city.Residents)
            {
                int job = u.Job[i] - 1;
                if (job < 0 || job == ids.JobLeader) continue;
                if (!CanWork(u, i) || city.JobCount[job] >= Desired[job])
                {
                    u.Job[i] = 0;
                    u.WorkBuilding[i] = -1;
                    UnitActSystem.Unload(civ, city, i);
                    continue;
                }
                city.JobCount[job]++;
            }

            Idle.Clear();
            foreach (int i in city.Residents)
                if (u.Job[i] == 0 && CanWork(u, i)) Idle.Add(i);
            int next = 0;
            for (int j = 0; j < city.JobCount.Length && next < Idle.Count; j++)
                while (next < Idle.Count && city.JobCount[j] < Desired[j])
                {
                    int i = Idle[next++];
                    u.Job[i] = (byte)(j + 1);
                    u.WorkBuilding[i] = -1;
                    city.JobCount[j]++;
                }
        }

        static int[] Desired = new int[64];

        public static bool CanWork(UnitStore u, int i)
        {
            var stage = (AgeStage)u.Age[i];
            return stage == AgeStage.Adult || stage == AgeStage.Elder;
        }
    }

    // Bölüm 5.4 planner: every 120 ticks per city, at most one new site per run and three at a time.
    public sealed class CityPlannerSystem : ISimSystem
    {
        public const int PlanEvery = 120, MaxConstructions = 3;

        public SimPhase Phase => SimPhase.Civ;
        public int Order => 30;

        public void Tick(in SimContext ctx)
        {
            var civ = ctx.Units.Civ;
            if (civ == null) return;
            long tick = ctx.Clock.Tick;
            ref var rng = ref ctx.Rng.Get(RngStream.Civ);
            for (int c = 0; c < civ.Cities.Count; c++)
            {
                var city = civ.Cities[c];
                if (city.Dead || tick < city.NextPlanTick) continue;
                city.NextPlanTick = tick + PlanEvery;
                CivMonthlySystem.Capacities(civ, city);
                if (city.ActiveConstructions >= MaxConstructions || city.Residents.Count == 0) continue;
                CityPlanner.PlanOne(ctx, civ, city, ref rng);
            }
        }
    }

    // Bölüm 5.3 border growth, settler groups, ruins fading. Yearly per city, staggered by city index.
    public sealed class CivYearlySystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.Civ;
        public int Order => 40;

        public void Tick(in SimContext ctx)
        {
            var civ = ctx.Units.Civ;
            if (civ == null) return;
            long tick = ctx.Clock.Tick;
            ref var rng = ref ctx.Rng.Get(RngStream.Civ);
            long inYear = tick % SimConst.TicksPerYear;
            for (int c = 0; c < civ.Cities.Count; c++)
            {
                var city = civ.Cities[c];
                if (city.Dead || (c * 37) % SimConst.TicksPerYear != inYear) continue;
                CityPlanner.GrowBorders(civ, city);
                Settlers.TrySend(ctx, civ, city, ref rng);
            }
            if (inYear == 0) FadeRuins(civ, tick);
        }

        static void FadeRuins(CivState civ, long tick)
        {
            for (int b = 0; b < civ.Buildings.Length; b++)
            {
                var data = civ.Buildings[b];
                if (data.State == BuildingState.Ruin && tick - data.StateTick >= CivState.RuinYears * SimConst.TicksPerYear) civ.Remove(b);
            }
        }
    }

    // Burning buildings lose 4 hp a tick (Bölüm 2.8, checked every 20 ticks); flooded or molten footprints collapse (5.12 #5).
    public sealed class BuildingUpkeepSystem : ISimSystem
    {
        public const int Every = 20;

        public SimPhase Phase => SimPhase.Civ;
        public int Order => 50;

        public void Tick(in SimContext ctx)
        {
            var civ = ctx.Units.Civ;
            if (civ == null) return;
            long tick = ctx.Clock.Tick;
            var map = ctx.World;
            for (int b = 0; b < civ.Buildings.Length; b++)
            {
                if ((b + tick) % Every != 0) continue;
                var data = civ.Buildings[b];
                if (data.State != BuildingState.Construction && data.State != BuildingState.Complete) continue;
                int burning = 0, broken = 0;
                for (int y = data.Origin.y; y < data.Origin.y + data.H; y++)
                    for (int x = data.Origin.x; x < data.Origin.x + data.W; x++)
                    {
                        int i = map.Index(x, y);
                        ushort f = map.Flags[i];
                        if ((f & (ushort)TileFlags.Burning) != 0) burning++;
                        if ((f & (ushort)TileFlags.Buildable) == 0 || (f & (ushort)TileFlags.Water) != 0) broken++;
                    }
                if (broken > 0)
                {
                    civ.Destroy(b, tick, ctx.Events, leaveRuins: false);
                    continue;
                }
                if (burning > 0)
                {
                    data.Hp -= 4f * Every;
                    data.Flags |= BuildingFlags.Burning;
                    civ.Buildings[b] = data;
                    if (data.Hp <= 0f) civ.Destroy(b, tick, ctx.Events, leaveRuins: true);
                }
                else if ((data.Flags & BuildingFlags.Burning) != 0)
                {
                    data.Flags &= ~BuildingFlags.Burning;
                    civ.Buildings[b] = data;
                }
            }
        }
    }
}
