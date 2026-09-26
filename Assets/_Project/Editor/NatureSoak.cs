using System;
using System.Diagnostics;
using System.IO;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PG.EditorTools
{
    // Bölüm 2.14 smoke numbers without tests: long runs with era/disaster/fire/ice counters and per-tick cost.
    // Batch: Unity -batchmode -projectPath . -executeMethod PG.EditorTools.NatureSoak.Run -logFile Logs/soak.log
    public static class NatureSoak
    {
        [MenuItem("PixelGenesis/Nature Soak")]
        public static void Run()
        {
            int failures = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                failures += Soak(db, MapSizePreset.Titanic, 20, 1);
                failures += Soak(db, MapSizePreset.Medium, 150, 3);
                failures += IceEra(db);
                failures += FireWind(db);
                failures += Ecosystem(db);
                failures += UnitPerf(db);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            Debug.Log(failures == 0 ? "[Soak] ALL OK" : $"[Soak] {failures} failure(s)");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        [MenuItem("PixelGenesis/Unit Soak")]
        public static void RunUnits()
        {
            int failures = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                failures += Ecosystem(db);
                failures += UnitPerf(db);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            Debug.Log(failures == 0 ? "[Soak] ALL OK" : $"[Soak] {failures} failure(s)");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static int Soak(ContentDB db, MapSizePreset size, int years, float disasterSlider)
        {
            var settings = new WorldGenSettings { Seed = 1234, Size = size };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                var n = sim.Nature;
                n.Laws.Set(n.LawAutoDisasters, true, disasterSlider);
                int eras = 0, disasters = 0, sameEraTwice = 0, lastEra = n.Era.CurrentEra;
                sim.Events.Subscribe<EraChangedEvent>(e => { eras++; if (e.NewEra == lastEra) sameEraTwice++; lastEra = e.NewEra; });
                sim.Events.Subscribe<DisasterRequestEvent>(e => disasters++);
                int features0 = CountFeatures(map), biomes0 = CountBiomes(map);

                var sw = Stopwatch.StartNew();
                double worst = 0;
                long ticks = (long)years * SimConst.TicksPerYear;
                int maxBurning = 0, maxClouds = 0;
                for (long t = 0; t < ticks; t++)
                {
                    sim.Tick();
                    worst = Math.Max(worst, sim.Pipeline.LastTickMs);
                    maxBurning = Math.Max(maxBurning, n.Burning.Length);
                    maxClouds = Math.Max(maxClouds, n.Clouds.Length);
                }
                double ms = sw.Elapsed.TotalMilliseconds;
                Debug.Log($"[Soak] {size} {map.Width}x{map.Height} {years}y: {ms / ticks:0.000} ms/tick avg, worst {worst:0.0} ms | eras {eras} " +
                          $"(repeat {sameEraTwice}) now {n.CurrentEra.Id} | disasters {disasters} | burning max {maxBurning} now {n.Burning.Length} " +
                          $"| clouds max {maxClouds} | lava {n.Lava.Length} | features {features0}->{CountFeatures(map)} | biome tiles {biomes0}->{CountBiomes(map)}");
                return sameEraTwice > 0 ? 1 : 0;
            }
        }

        // 2.14 #6: forced ice era -> most shallow water freezes within 3 years.
        static int IceEra(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 99, Size = MapSizePreset.Small };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                int shallow = db.Tiles.IdOf("tile.shallow"), ice = db.Tiles.IdOf("tile.ice");
                int before = Count(map, shallow);
                var rng = new SimRandom(1, 1);
                EraSystem.SetEra(sim.Nature, db.Eras.IdOf("era.ice"), 0, sim.Events, ref rng);
                sim.Nature.Era.Frozen = true;
                for (int t = 0; t < 3 * SimConst.TicksPerYear; t++) sim.Tick();
                int frozen = Count(map, ice);
                float ratio = before > 0 ? (float)frozen / before : 0f;
                Debug.Log($"[Soak] ice era: shallow {before} -> ice {frozen} ({ratio:P0}), biome growth mul {sim.Nature.Mods.BiomeGrowthMul}");
                return 0;
            }
        }

        // 2.14 #3: a burning forest spreads further downwind than upwind.
        static int FireWind(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 5, Size = MapSizePreset.Small, Template = "wgt.flat_green" };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                var tree = db.Features[db.Features.IdOf("feat.tree_oak")].MapValue;
                int cx = map.Width / 2, cy = map.Height / 2;
                for (int y = cy - 40; y <= cy + 40; y++)
                    for (int x = cx - 60; x <= cx + 60; x++)
                        if (map.InBounds(x, y)) map.SetFeature(x, y, tree, 2, ChangeSource.Power);
                var n = sim.Nature;
                n.WindAngle = 0f; // blowing east (+x)
                n.WindSpeed = 0.8f;
                n.Ignite(cx, cy, 255);
                for (int t = 0; t < 400; t++)
                {
                    sim.Tick();
                    n.WindAngle = 0f;
                    n.WindSpeed = 0.8f;
                }
                int east = 0, west = 0;
                for (int y = cy - 40; y <= cy + 40; y++)
                    for (int x = cx - 60; x <= cx + 60; x++)
                    {
                        int i = map.Index(x, y);
                        bool burnt = map.Feature[i] != tree;
                        if (!burnt) continue;
                        if (x > cx) east++;
                        else if (x < cx) west++;
                    }
                Debug.Log($"[Soak] fire+wind: burnt east {east} west {west} (ratio {(west > 0 ? (float)east / west : east):0.0})");
                return east > west ? 0 : 1;
            }
        }

        // 3.12 #4-5: sheep + wolves on a generated island world, populations over the years.
        static int Ecosystem(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 314, Size = MapSizePreset.Small };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                int sheep = db.Species.IdOf("sp.sheep"), wolf = db.Species.IdOf("sp.wolf");
                Spawn(sim, sheep, 50, 11);
                Spawn(sim, wolf, 6, 12);
                int births = 0, deaths = 0;
                var causes = new int[16];
                sim.Events.Subscribe<UnitBornEvent>(e => births++);
                var starvedBy = new int[db.Species.Count];
                sim.Events.Subscribe<UnitDiedEvent>(e => { deaths++; causes[(int)e.Cause]++; if (e.Cause == DeathCause.Starved) starvedBy[e.Species]++; });
                var line = new System.Text.StringBuilder();
                var sw = Stopwatch.StartNew();
                for (int year = 1; year <= 40; year++)
                {
                    for (int t = 0; t < SimConst.TicksPerYear; t++) sim.Tick();
                    if (year % 5 == 0) line.Append($" y{year}:{Count(sim, sheep)}/{Count(sim, wolf)}");
                }
                Debug.Log($"[Soak] ecosystem sheep/wolf{line} | births {births} deaths {deaths} (old {causes[(int)DeathCause.OldAge]}, killed {causes[(int)DeathCause.Killed]}, " +
                          $"starved {causes[(int)DeathCause.Starved]} [{TopStarved(db, starvedBy)}], drowned {causes[(int)DeathCause.Drowned]}) | total units {sim.Units.Store.Count} | {sw.Elapsed.TotalMilliseconds / (40.0 * SimConst.TicksPerYear):0.000} ms/tick");
                return 0;
            }
        }

        [MenuItem("PixelGenesis/Feed Debug")]
        public static void FeedDebug()
        {
            DataSync.Sync();
            var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
            var settings = new WorldGenSettings { Seed = 314, Size = MapSizePreset.Small };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
                string feedSpecies = System.Environment.GetEnvironmentVariable("PG_FEED_SPECIES") ?? "sp.sheep";
                int sheep = db.Species.IdOf(feedSpecies);
                Spawn(sim, sheep, 30, 11);
                var u = sim.Units.Store;
                int births = 0;
                var causes = new int[16];
                sim.Events.Subscribe<UnitBornEvent>(e => { if (e.Species == sheep) births++; });
                sim.Events.Subscribe<UnitDiedEvent>(e => { if (e.Species == sheep) causes[(int)e.Cause]++; });
                for (int year = 1; year <= 20; year++)
                {
                    for (int t = 0; t < SimConst.TicksPerYear; t++) sim.Tick();
                    int n = 0, wants = 0, pregnant = 0, adults = 0, females = 0;
                    float sat = 0;
                    var tasks = new int[12];
                    for (int k = 0; k < u.Alive.Length; k++)
                    {
                        int i = u.Alive[k];
                        if (u.Species[i] != sheep) continue;
                        n++;
                        sat += u.Saturation[i];
                        tasks[u.Task[i]]++;
                        if ((u.Wants[i] & 1) != 0) wants++;
                        if (u.HasStatus(i, sim.Units.StPregnant)) pregnant++;
                        if (u.Age[i] == (byte)AgeStage.Adult) adults++;
                        if (u.Sex[i] == 0) females++;
                    }
                    Debug.Log($"[Feed] y{year} sheep {n} adults {adults} females {females} sat {sat / math.max(1, n):0} wants {wants} pregnant {pregnant} births {births} " +
                              $"deaths " + string.Join(",", causes) + " tasks " + string.Join(",", tasks));
                }
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static string TopStarved(ContentDB db, int[] counts)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int k = 0; k < 4; k++)
            {
                int best = -1;
                for (int s = 0; s < counts.Length; s++) if (counts[s] > 0 && (best < 0 || counts[s] > counts[best])) best = s;
                if (best < 0) break;
                parts.Add(db.Species[best].Id + " " + counts[best]);
                counts[best] = 0;
            }
            return string.Join(", ", parts);
        }

        // 3.12 #7: 5000 units at x1; unit systems time per tick.
        static int UnitPerf(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 77, Size = MapSizePreset.Huge };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                string[] kinds = { "sp.sheep", "sp.cow", "sp.rabbit", "sp.deer", "sp.wolf", "sp.bear", "sp.fox", "sp.boar" };
                for (int k = 0; k < kinds.Length; k++) Spawn(sim, db.Species.IdOf(kinds[k]), 5000 / kinds.Length, 100UL + (ulong)k);
                for (int t = 0; t < 200; t++) sim.Tick(); // warm up (paths, Burst)
                double unitMs = 0, worst = 0;
                const int ticks = 600;
                for (int t = 0; t < ticks; t++)
                {
                    sim.Tick();
                    double tickUnits = 0;
                    for (int s = 0; s < sim.Pipeline.Count; s++)
                    {
                        var phase = sim.Pipeline[s].Phase;
                        if (phase == SimPhase.UnitsThink || phase == SimPhase.UnitsAct || sim.Pipeline[s] is UnitCleanupSystem) tickUnits += sim.Pipeline.LastMs(s);
                    }
                    unitMs += tickUnits;
                    worst = Math.Max(worst, tickUnits);
                }
                Debug.Log($"[Soak] unit perf: {sim.Units.Store.Count} units, unit systems {unitMs / ticks:0.00} ms/tick avg, worst {worst:0.0} ms, paths/tick budget {PathService.RequestsPerTick}");
                return 0;
            }
        }

        static void Spawn(SimWorld sim, int species, int count, ulong seed)
        {
            var rng = new SimRandom(seed, 5);
            var map = sim.World;
            for (int n = 0, tries = 0; n < count && tries < count * 50; tries++)
            {
                int x = rng.Range(0, map.Width), y = rng.Range(0, map.Height);
                if (!map.IsWalkable(x, y) || map.IsWater(x, y)) continue;
                float life = sim.Content.Species[species].BaseStats[(int)StatId.Lifespan];
                sim.Units.Store.Spawn(new UnitSpawnRequest
                {
                    Species = species, Pos = new Unity.Mathematics.float2(x + 0.5f, y + 0.5f), AgeYears = life * rng.Range(0.2f, 0.6f),
                    Sex = n % 2, Mother = PG.Core.EntityId.None, Father = PG.Core.EntityId.None, Subspecies = -1,
                }, sim.Clock.Tick, ref rng);
                n++;
            }
        }

        static int Count(SimWorld sim, int species)
        {
            int c = 0;
            var u = sim.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++) if (u.Species[u.Alive[k]] == species && u.State[u.Alive[k]] == UnitStore.StateAlive) c++;
            return c;
        }

        static int Count(WorldMap map, int tile)
        {
            int c = 0;
            for (int i = 0; i < map.TileCount; i++) if (map.Ground[i] == tile) c++;
            return c;
        }

        static int CountFeatures(WorldMap map)
        {
            int c = 0;
            for (int i = 0; i < map.TileCount; i++) if (map.Feature[i] != 0) c++;
            return c;
        }

        static int CountBiomes(WorldMap map)
        {
            int c = 0;
            for (int i = 0; i < map.TileCount; i++) if (map.Biome[i] != 0) c++;
            return c;
        }
    }
}
