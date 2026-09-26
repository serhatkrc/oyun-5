using System;
using System.IO;
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
    // Bölüm 3.12 acceptance checks that need long simulations (#4 predator/prey, #5 starvation, #9 determinism).
    // Batch: Unity -batchmode -projectPath . -executeMethod PG.EditorTools.UnitAcceptance.Run -logFile Logs/units.log
    public static class UnitAcceptance
    {
        static readonly ulong[] Seeds = { 11, 22, 33, 44, 55 };

        [MenuItem("PixelGenesis/Unit Acceptance")]
        public static void Run()
        {
            int failures = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                failures += PredatorPrey(db);
                failures += Starvation(db);
                failures += Determinism(db);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            Debug.Log(failures == 0 ? "[UnitAcceptance] ALL OK" : $"[UnitAcceptance] {failures} failure(s)");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // #4: 50 sheep + 5 wolves on one island, no natural spawns, 100 years; neither dies out within 30 years in 4 of 5 seeds.
        public static int PredatorPrey(ContentDB db)
        {
            int passed = 0;
            foreach (ulong seed in Seeds)
            {
                var settings = new WorldGenSettings { Seed = seed, Size = MapSizePreset.Small, Template = "wgt.pangea", LandRatio = 0.5f };
                var map = WorldGenerator.Generate(settings, db);
                using (var sim = new SimWorld(db, map, new SimRandomProvider(seed)))
                {
                    sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
                    int sheep = db.Species.IdOf("sp.sheep"), wolf = db.Species.IdOf("sp.wolf");
                    int2 center = LargestIslandPoint(sim);
                    Spawn(sim, sheep, 50, center, 30, seed * 7 + 1);
                    Spawn(sim, wolf, 5, center, 30, seed * 7 + 2);
                    int sheepZero = -1, wolfZero = -1, wolfBirths = 0;
                    var wolfDeaths = new int[16];
                    sim.Events.Subscribe<UnitDiedEvent>(e => { if (e.Species == wolf) wolfDeaths[(int)e.Cause]++; });
                    sim.Events.Subscribe<UnitBornEvent>(e => { if (e.Species == wolf) wolfBirths++; });
                    var line = new System.Text.StringBuilder();
                    for (int year = 1; year <= 100; year++)
                    {
                        for (int t = 0; t < SimConst.TicksPerYear; t++) sim.Tick();
                        int s = Count(sim, sheep), w = Count(sim, wolf);
                        if (s == 0 && sheepZero < 0) sheepZero = year;
                        if (w == 0 && wolfZero < 0) wolfZero = year;
                        if (year % 10 == 0) line.Append($" y{year}:{s}/{w}");
                    }
                    bool ok = (sheepZero < 0 || sheepZero > 30) && (wolfZero < 0 || wolfZero > 30);
                    if (ok) passed++;
                    Debug.Log($"[UnitAcceptance] predator/prey seed {seed}:{line} | sheep extinct {Year(sheepZero)}, wolves extinct {Year(wolfZero)} (wolf births {wolfBirths}, starved {wolfDeaths[(int)DeathCause.Starved]}, killed {wolfDeaths[(int)DeathCause.Killed]}, old {wolfDeaths[(int)DeathCause.OldAge]}, other {wolfDeaths[(int)DeathCause.Drowned] + wolfDeaths[(int)DeathCause.Burned] + wolfDeaths[(int)DeathCause.Frozen] + wolfDeaths[(int)DeathCause.Disease]}) -> {(ok ? "ok" : "FAIL")}");
                }
            }
            Debug.Log($"[UnitAcceptance] predator/prey: {passed}/{Seeds.Length} seeds keep both species for 30 years (need 4)");
            return passed >= 4 ? 0 : 1;
        }

        // #5: sheep with nothing to eat starve in about a year.
        public static int Starvation(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 3, Size = MapSizePreset.Tiny, Template = "wgt.flat_green", ForestDensity = 0f };
            var map = WorldGenerator.Generate(settings, db);
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    if (map.Biome[map.Index(x, y)] != 0) map.SetBiome(x, y, 0, ChangeSource.Power);
                    if (map.Feature[map.Index(x, y)] != 0) map.SetFeature(x, y, 0, ChangeSource.Power);
                }
            using (var sim = new SimWorld(db, map, new SimRandomProvider(3)))
            {
                var laws = sim.Nature.Laws;
                laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
                laws.Set(sim.Nature.LawBiomeSpread, false, 0f);
                laws.Set(sim.Nature.LawTreeGrowth, false, 0f);
                int sheep = db.Species.IdOf("sp.sheep");
                Spawn(sim, sheep, 20, new int2(map.Width / 2, map.Height / 2), 20, 99);
                int months = 0;
                while (Count(sim, sheep) > 0 && months < 36)
                {
                    for (int t = 0; t < SimConst.TicksPerMonth; t++) sim.Tick();
                    months++;
                }
                bool ok = months >= 9 && months <= 16;
                Debug.Log($"[UnitAcceptance] starvation: all sheep dead after {months} months (target ~12) -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        // #9: 5,000 ticks with 2,000 units twice -> identical unit and world hashes.
        public static int Determinism(ContentDB db)
        {
            ulong a = DeterminismRun(db), b = DeterminismRun(db);
            bool ok = a == b;
            Debug.Log($"[UnitAcceptance] determinism 5000 ticks / 2000 units: {a:X16} vs {b:X16} -> {(ok ? "ok" : "FAIL")}");
            return ok ? 0 : 1;
        }

        static ulong DeterminismRun(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 2024, Size = MapSizePreset.Medium };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                string[] kinds = { "sp.sheep", "sp.cow", "sp.rabbit", "sp.deer", "sp.wolf", "sp.bear", "sp.chicken", "sp.frog" };
                for (int k = 0; k < kinds.Length; k++)
                {
                    int species = db.Species.IdOf(kinds[k]);
                    if (species >= 0) Spawn(sim, species, 250, new int2(map.Width / 2, map.Height / 2), map.Width / 2, 500UL + (ulong)k);
                }
                ulong h = 0;
                for (int t = 1; t <= 5000; t++)
                {
                    sim.Tick();
                    if (t % 500 == 0) h = (h * 1099511628211UL) ^ sim.World.ComputeHash() ^ PG.Boot.Diagnostics.UnitHash(sim);
                }
                return h;
            }
        }

        static string Year(int y) => y < 0 ? "never" : "year " + y;

        // A land tile near the middle of the biggest island (sampled).
        static int2 LargestIslandPoint(SimWorld sim)
        {
            var map = sim.World;
            int2 best = new int2(map.Width / 2, map.Height / 2);
            float bestD = float.MaxValue;
            for (int y = 0; y < map.Height; y += 4)
                for (int x = 0; x < map.Width; x += 4)
                {
                    if (!map.IsWalkable(x, y) || map.IsWater(x, y)) continue;
                    float d = math.distance(new float2(x, y), new float2(map.Width / 2f, map.Height / 2f));
                    if (d < bestD) { bestD = d; best = new int2(x, y); }
                }
            return best;
        }

        static void Spawn(SimWorld sim, int species, int count, int2 around, int radius, ulong seed)
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
                    Species = species, Pos = new float2(x + 0.5f, y + 0.5f), AgeYears = life * rng.Range(0.2f, 0.5f),
                    Sex = n % 2, Mother = EntityId.None, Father = EntityId.None, Subspecies = -1,
                }, sim.Clock.Tick, ref rng);
                n++;
            }
        }

        static int Count(SimWorld sim, int species)
        {
            int c = 0;
            var u = sim.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
                if (u.Species[u.Alive[k]] == species && u.State[u.Alive[k]] == UnitStore.StateAlive) c++;
            return c;
        }
    }
}
