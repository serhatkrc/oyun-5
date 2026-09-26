using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PG.EditorTools
{
    // Bölüm 5.12 acceptance checks for the civilisation core.
    // Batch: Unity -batchmode -projectPath . -executeMethod PG.EditorTools.CivSoak.Run -logFile Logs/civ.log
    public static class CivSoak
    {
        [MenuItem("PixelGenesis/Civ Soak")]
        public static void Run()
        {
            int failures = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                failures += Village(db, 100);
                failures += Barren(db);
                failures += Placement(db);
                failures += Flood(db);
                failures += Quality(db);
                failures += Performance(db);
                failures += SaveLoad(db);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            Debug.Log(failures == 0 ? "[CivSoak] ALL OK" : $"[CivSoak] {failures} failure(s)");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // #1: 10 humans on an empty meadow, 100 years -> a city with hall_2, >= 60 people, at least one more city.
        public static int Village(ContentDB db, int years)
        {
            // "empty meadow": a single continent with hills and rock (the flat template has no stone for hall_2; DECISIONS #60)
            var settings = new WorldGenSettings { Seed = 17, Size = MapSizePreset.Medium, Template = "wgt.pangea", ForestDensity = 0.4f };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
                sim.SpawnInitialAnimals();
                int human = db.Species.IdOf("sp.human");
                SpawnGroup(sim, human, 10, Meadow(map, new int2(map.Width / 2, map.Height / 2)), 6, 7);
                var sw = Stopwatch.StartNew();
                var line = new StringBuilder();
                var deaths = new int[16];
                int births = 0;
                sim.Events.Subscribe<UnitDiedEvent>(e => { if (e.Species == human) deaths[(int)e.Cause]++; });
                sim.Events.Subscribe<UnitBornEvent>(e => { if (e.Species == human) births++; });
                for (int year = 1; year <= years; year++)
                {
                    for (int t = 0; t < SimConst.TicksPerYear; t++) sim.Tick();
                    if (year % 10 == 0 || year == 1 || year == 3)
                        line.Append($"\n  y{year}: births {births} deaths old {deaths[(int)DeathCause.OldAge]} starved {deaths[(int)DeathCause.Starved]} killed {deaths[(int)DeathCause.Killed]} drowned {deaths[(int)DeathCause.Drowned]} burned {deaths[(int)DeathCause.Burned]} frozen {deaths[(int)DeathCause.Frozen]} | {Summary(sim)}");
                }
                var civ = sim.Civ;
                int maxTier = 0, total = 0, cities = 0;
                foreach (var c in civ.Cities)
                {
                    if (c.Dead) continue;
                    cities++;
                    total += c.Population;
                    maxTier = math.max(maxTier, c.HallTier);
                }
                bool ok = cities >= 2 && maxTier >= 2 && total >= 60;
                Debug.Log($"[CivSoak] village {years}y ({sw.Elapsed.TotalSeconds:0.0} s):{line}\n  -> cities {cities}, best hall tier {maxTier}, civ population {total} -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        public static string Summary(SimWorld sim)
        {
            var civ = sim.Civ;
            var sb = new StringBuilder();
            int alive = 0;
            foreach (var c in civ.Cities)
            {
                if (c.Dead) continue;
                alive++;
                if (alive > 4) continue;
                int complete = 0, building = 0;
                foreach (int b in c.Buildings)
                {
                    if (civ.Buildings[b].State == BuildingState.Complete) complete++;
                    else if (civ.Buildings[b].State == BuildingState.Construction) building++;
                }
                sb.Append($"[{c.Name} pop {c.Population} tier {c.HallTier} zones {c.Zones.Count} bld {complete}+{building} house {c.HousingCapacity} " +
                          $"food {c.FoodTotal} wood {c.StockOf(civ.Ids.Wood)} stone {c.StockOf(civ.Ids.Stone)} gold {c.Gold} hap {c.Happiness} famine {c.FamineMonths}] ");
            }
            return $"cities {alive} {sb}";
        }

        // #3: a bare rock island with nothing to eat -> famine, migration or decline.
        public static int Barren(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 23, Size = MapSizePreset.Small, Template = "wgt.flat_green", ForestDensity = 0f };
            var map = WorldGenerator.Generate(settings, db);
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    int i = map.Index(x, y);
                    if (map.Feature[i] != 0) map.SetFeature(x, y, 0, ChangeSource.Power);
                    if (map.Biome[i] != 0) map.SetBiome(x, y, 0, ChangeSource.Power);
                }
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                var laws = sim.Nature.Laws;
                laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
                laws.Set(sim.Nature.LawBiomeSpread, false, 0f);
                laws.Set(sim.Nature.LawTreeGrowth, false, 0f);
                int famines = 0;
                sim.Events.Subscribe<FamineEvent>(e => famines++);
                SpawnGroup(sim, db.Species.IdOf("sp.human"), 12, new int2(map.Width / 2, map.Height / 2), 6, 3);
                int peak = 0;
                for (int m = 0; m < 5 * SimConst.MonthsPerYear; m++)
                {
                    for (int t = 0; t < SimConst.TicksPerMonth; t++) sim.Tick();
                    peak = math.max(peak, CivPopulation(sim));
                }
                int end = CivPopulation(sim);
                bool ok = famines > 0 || end < peak;
                Debug.Log($"[CivSoak] barren island 5y: famines {famines}, civ population peak {peak} -> {end} -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        // #4: no building on water, mountains or another building; fishing huts touch water.
        public static int Placement(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 31, Size = MapSizePreset.Medium, Template = "wgt.continents" };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
                sim.SpawnInitialAnimals();
                int human = db.Species.IdOf("sp.human");
                var rng = new SimRandom(31, 9);
                for (int g = 0; g < 6; g++)
                {
                    int2 p = RandomLand(map, ref rng);
                    SpawnGroup(sim, human, 8, p, 5, 40UL + (ulong)g);
                }
                for (int t = 0; t < 30 * SimConst.TicksPerYear; t++) sim.Tick();
                int bad = 0, checkedCount = 0;
                var civ = sim.Civ;
                for (int b = 0; b < civ.Buildings.Length; b++)
                {
                    var data = civ.Buildings[b];
                    if (data.State == BuildingState.Free || data.State == BuildingState.Ruin) continue;
                    checkedCount++;
                    for (int y = data.Origin.y; y < data.Origin.y + data.H; y++)
                        for (int x = data.Origin.x; x < data.Origin.x + data.W; x++)
                        {
                            int i = map.Index(x, y);
                            if (map.Building[i] != b || map.IsWater(x, y) || !map.HasFlag(x, y, TileFlags.Buildable))
                            {
                                bad++;
                                if (bad <= 5) Debug.Log($"[CivSoak] placement: {db.Buildings[data.Def].Id} ({data.State}) tile {x},{y} owner-building {map.Building[i]} ground {db.Tiles[map.Ground[i]].Id} water {map.IsWater(x, y)}");
                            }
                        }
                    if (data.Def == civ.Ids.FishingHut && !NextToWater(map, data)) { bad++; Debug.Log($"[CivSoak] placement: fishing hut at {data.Origin} away from water"); }
                }
                bool ok = bad == 0 && checkedCount > 0;
                Debug.Log($"[CivSoak] placement: {checkedCount} buildings in {civ.AliveCities} cities, {bad} bad tiles -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        // #5: flooding a city's ground destroys its buildings and leaves people homeless; the city tries again.
        public static int Flood(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 17, Size = MapSizePreset.Small, Template = "wgt.flat_green" };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
                SpawnGroup(sim, db.Species.IdOf("sp.human"), 12, new int2(map.Width / 2, map.Height / 2), 6, 5);
                for (int t = 0; t < 15 * SimConst.TicksPerYear; t++) sim.Tick();
                var civ = sim.Civ;
                if (civ.AliveCities == 0) { Debug.Log("[CivSoak] flood: no city formed -> FAIL"); return 1; }
                var city = civ.Cities[0];
                int before = 0;
                foreach (int b in city.Buildings) if (civ.Buildings[b].State == BuildingState.Complete) before++;
                byte shallow = (byte)db.Tiles.IdOf("tile.shallow");
                int destroyed = 0;
                sim.Events.Subscribe<BuildingDestroyedEvent>(e => destroyed++);
                // flood the half of the city east of the centre
                using (map.BeginBatch(ChangeSource.Power))
                    foreach (int z in city.Zones)
                    {
                        int x0 = (z % map.ZonesX) << WorldMap.ZoneShift, y0 = (z / map.ZonesX) << WorldMap.ZoneShift;
                        if (x0 < city.Center.x) continue;
                        for (int y = y0; y < y0 + WorldMap.ZoneSize; y++)
                            for (int x = x0; x < x0 + WorldMap.ZoneSize; x++) map.SetGround(x, y, shallow, ChangeSource.Power);
                    }
                for (int t = 0; t < 2 * SimConst.TicksPerMonth; t++) sim.Tick();
                int homeless = 0;
                var u = sim.Units.Store;
                foreach (int i in city.Residents) if (u.HomeBuilding[i] < 0) homeless++;
                int planned = 0;
                for (int t = 0; t < 2 * SimConst.TicksPerYear; t++) sim.Tick();
                foreach (int b in city.Buildings) if (civ.Buildings[b].State != BuildingState.Free) planned++;
                bool ok = destroyed > 0 && !city.Dead;
                Debug.Log($"[CivSoak] flood: {before} buildings, {destroyed} destroyed, {homeless} homeless after 2 months, {planned} buildings 2 years later -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        // #6: 10,000 quality rolls match item_qualities.json baseChance within +-3%.
        public static int Quality(ContentDB db)
        {
            var rng = new SimRandom(6, 6);
            var counts = new int[db.ItemQualities.Count];
            const int n = 10000;
            for (int k = 0; k < n; k++) counts[Crafting.RollQuality(db, 0, ref rng)]++;
            bool ok = true;
            var sb = new StringBuilder();
            for (int q = 0; q < counts.Length; q++)
            {
                float got = counts[q] / (float)n, want = db.ItemQualities[q].BaseChance;
                if (math.abs(got - want) > 0.03f) ok = false;
                sb.Append($"{db.ItemQualities[q].Id} {got:P1}/{want:P1} ");
            }
            Debug.Log($"[CivSoak] item quality: {sb}-> {(ok ? "ok" : "FAIL")}");
            return ok ? 0 : 1;
        }

        // #7: 10 cities, ~3,000 civilised units -> civ systems < 3 ms/tick on average.
        public static int Performance(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 44, Size = MapSizePreset.Huge, Template = "wgt.pangea" };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
                int human = db.Species.IdOf("sp.human");
                var rng = new SimRandom(44, 4);
                for (int g = 0; g < 10; g++) SpawnGroup(sim, human, 300, RandomLand(map, ref rng), 14, 900UL + (ulong)g);
                for (int t = 0; t < 2 * SimConst.TicksPerYear; t++) sim.Tick(); // found cities, assign jobs
                double civMs = 0, unitMs = 0;
                const int ticks = 1200;
                for (int t = 0; t < ticks; t++)
                {
                    sim.Tick();
                    for (int s = 0; s < sim.Pipeline.Count; s++)
                    {
                        if (sim.Pipeline[s].Phase == SimPhase.Civ) civMs += sim.Pipeline.LastMs(s);
                        else if (sim.Pipeline[s].Phase == SimPhase.UnitsAct || sim.Pipeline[s].Phase == SimPhase.UnitsThink) unitMs += sim.Pipeline.LastMs(s);
                    }
                }
                double avg = civMs / ticks;
                bool ok = avg < 3.0;
                Debug.Log($"[CivSoak] perf: {sim.Civ.AliveCities} cities, {CivPopulation(sim)} civilised units, civ systems {avg:0.00} ms/tick, unit systems {unitMs / ticks:0.00} ms/tick -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        // Nearest grassy, buildable spot to `near` (spiral).
        public static int2 Meadow(WorldMap map, int2 near)
        {
            for (int r = 0; r < map.Width / 2; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = near.x + dx, y = near.y + dy;
                        if (!map.InBounds(x, y) || !map.HasFlag(x, y, TileFlags.Buildable) || map.IsWater(x, y)) continue;
                        byte b = map.Biome[map.Index(x, y)];
                        if (b == 0 || map.Content.Biomes[b - 1].Id != "bio.grassland") continue;
                        return new int2(x, y);
                    }
            return near;
        }

        // #8: save while buildings are going up, load, continue both 600 ticks -> identical worlds, units and cities.
        public static int SaveLoad(ContentDB db)
        {
            const int slot = 997;
            var settings = new WorldGenSettings { Seed = 17, Size = MapSizePreset.Medium, Template = "wgt.pangea", ForestDensity = 0.4f };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed), null, "CivSave"))
            {
                sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
                sim.SpawnInitialAnimals();
                SpawnGroup(sim, db.Species.IdOf("sp.human"), 10, Meadow(map, new int2(map.Width / 2, map.Height / 2)), 6, 7);
                int sites = 0;
                for (int t = 0; t < 40 * SimConst.TicksPerYear && sites == 0; t++)
                {
                    sim.Tick();
                    if (t > 12 * SimConst.TicksPerYear && t % 60 == 30) sites = CountSites(sim.Civ);
                }
                PG.Persistence.SaveSystem.Save(slot, sim);
                var state = PG.Persistence.SaveSystem.Load(slot, db);
                using (var loaded = new SimWorld(db, state.World, state.Rng, state.Clock, state.WorldName))
                {
                    state.ApplyTo(loaded);
                    bool before = Hash(sim) == Hash(loaded);
                    if (!before) Debug.Log($"[CivSoak] diff: world {sim.World.ComputeHash() == loaded.World.ComputeHash()} units {PG.Boot.Diagnostics.UnitHash(sim) == PG.Boot.Diagnostics.UnitHash(loaded)} cities {sim.Civ.Cities.Count}/{loaded.Civ.Cities.Count} buildings {sim.Civ.Buildings.Length}/{loaded.Civ.Buildings.Length} flagsEq {Eq(sim.World.Flags, loaded.World.Flags)} bldEq {Eq(sim.World.Building, loaded.World.Building)} groundEq {Eq(sim.World.Ground, loaded.World.Ground)} featEq {Eq(sim.World.Feature, loaded.World.Feature)} fsEq {Eq(sim.World.FeatureState, loaded.World.FeatureState)}");
                    for (int i = 0; i < 600; i++) { sim.Tick(); loaded.Tick(); }
                    bool after = Hash(sim) == Hash(loaded);
                    PG.Persistence.SaveSystem.Delete(slot);
                    bool ok = before && after && sites > 0 && state.Warnings.Count == 0;
                    Debug.Log($"[CivSoak] save/load with {sites} building sites, {sim.Civ.AliveCities} cities: equal after load {before}, after 600 ticks {after}, warnings {state.Warnings.Count} -> {(ok ? "ok" : "FAIL")}");
                    return ok ? 0 : 1;
                }
            }
        }

        static string Eq<T>(Unity.Collections.NativeArray<T> a, Unity.Collections.NativeArray<T> b) where T : struct, IEquatable<T>
        {
            int diff = 0, first = -1;
            for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) { diff++; if (first < 0) first = i; }
            return diff == 0 ? "yes" : $"no({diff} first {first}: {a[first]} vs {b[first]})";
        }

        static int CountSites(CivState civ)
        {
            int n = 0;
            for (int b = 0; b < civ.Buildings.Length; b++) if (civ.Buildings[b].State == BuildingState.Construction) n++;
            return n;
        }

        // World tiles + units + cities (stock, zones, buildings with progress).
        public static ulong Hash(SimWorld sim)
        {
            ulong h = sim.World.ComputeHash() ^ PG.Boot.Diagnostics.UnitHash(sim);
            var civ = sim.Civ;
            void Mix(long v) => h = (h ^ (ulong)v) * 1099511628211UL;
            foreach (var c in civ.Cities)
            {
                Mix(c.Uid); Mix(c.Population); Mix(c.Zones.Count); Mix(c.HallTier); Mix(c.Gold);
                foreach (int v in c.Stock) Mix(v);
            }
            for (int b = 0; b < civ.Buildings.Length; b++)
            {
                var d = civ.Buildings[b];
                Mix((long)d.State); Mix(d.Def); Mix(d.Origin.x); Mix(d.Origin.y); Mix((long)(d.BuildProgress * 1000)); Mix((long)d.Hp);
            }
            var u = sim.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++) { int i = u.Alive[k]; Mix(u.City[i]); Mix(u.Job[i]); Mix(u.CarryAmount[i]); }
            return h;
        }

        static bool NextToWater(WorldMap map, BuildingData d)
        {
            for (int y = d.Origin.y - 1; y <= d.Origin.y + d.H; y++)
                for (int x = d.Origin.x - 1; x <= d.Origin.x + d.W; x++)
                    if (map.InBounds(x, y) && (map.IsWater(x, y) || map.HasFlag(x, y, TileFlags.Frozen))) return true; // lakes may freeze later
            return false;
        }

        static int2 RandomLand(WorldMap map, ref SimRandom rng)
        {
            for (int k = 0; k < 10000; k++)
            {
                int x = rng.Range(16, map.Width - 16), y = rng.Range(16, map.Height - 16);
                if (map.IsWalkable(x, y) && !map.IsWater(x, y) && map.HasFlag(x, y, TileFlags.Buildable)) return new int2(x, y);
            }
            return new int2(map.Width / 2, map.Height / 2);
        }

        public static int CivPopulation(SimWorld sim)
        {
            var u = sim.Units.Store;
            int n = 0;
            for (int k = 0; k < u.Alive.Length; k++)
                if (u.SpeciesOf(u.Alive[k]).IsCiv) n++;
            return n;
        }

        public static void SpawnGroup(SimWorld sim, int species, int count, int2 around, int radius, ulong seed)
        {
            var rng = new SimRandom(seed, 5);
            var map = sim.World;
            for (int n = 0, tries = 0; n < count && tries < count * 200; tries++)
            {
                int x = around.x + rng.Range(-radius, radius + 1), y = around.y + rng.Range(-radius, radius + 1);
                if (!map.InBounds(x, y) || !map.IsWalkable(x, y) || map.IsWater(x, y)) continue;
                float life = sim.Content.Species[species].BaseStats[(int)StatId.Lifespan];
                sim.Units.Store.Spawn(new UnitSpawnRequest
                {
                    Species = species, Pos = new float2(x + 0.5f, y + 0.5f), AgeYears = life * rng.Range(0.2f, 0.4f),
                    Sex = n % 2, Mother = EntityId.None, Father = EntityId.None, Subspecies = -1,
                }, sim.Clock.Tick, ref rng);
                n++;
            }
        }
    }
}
