using System.Collections.Generic;
using System.Diagnostics;
using PG.Content;
using PG.Core;
using PG.Powers;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace PG.Boot
{
    // Bölüm 1.15 determinism check and EkB 3.7.3 headless run. Both work without rendering.
    public static class Diagnostics
    {
        const int HashEvery = 100;
        const int EditEvery = 5;

        // Two runs with the same seed and the same scripted brush edits must produce identical world hashes.
        public static bool DeterminismCheck(ContentDB db, WorldGenSettings settings, int ticks)
        {
            var a = Run(db, settings, ticks);
            var b = Run(db, settings, ticks);
            bool ok = a.Count == b.Count;
            for (int i = 0; ok && i < a.Count; i++)
            {
                if (a[i] == b[i]) continue;
                Debug.LogError($"[Determinism] Mismatch at tick {(i + 1) * HashEvery}: {a[i]:X16} != {b[i]:X16}");
                ok = false;
            }
            if (ok) Debug.Log($"[Determinism] OK: {ticks} ticks, {a.Count} hashes, last {(a.Count > 0 ? a[a.Count - 1] : 0):X16}");
            return ok;
        }

        static List<ulong> Run(ContentDB db, WorldGenSettings settings, int ticks)
        {
            var hashes = new List<ulong>();
            var map = WorldGenerator.Generate(settings.Clone(), db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                sim.SpawnInitialAnimals();
                var queue = new PowerCommandQueue();
                sim.Pipeline.Add(new PowerCommandSystem(queue, db));
                var brushes = BrushPowers(db);
                var script = new SimRandom(settings.Seed, 0xD37E);

                for (int t = 1; t <= ticks; t++)
                {
                    if (brushes.Count > 0 && t % EditEvery == 0) EnqueueRandomEdit(queue, brushes, map, ref script, sim.Clock.Tick);
                    sim.Tick();
                    if (t % HashEvery == 0) hashes.Add(sim.World.ComputeHash() ^ ((ulong)sim.Regions.IslandCount << 48) ^ UnitHash(sim));
                }
            }
            return hashes;
        }

        // Positions, hp and species of every live unit (Bölüm 3.12 #9).
        public static ulong UnitHash(SimWorld sim)
        {
            var u = sim.Units.Store;
            ulong h = 1469598103934665603UL ^ (ulong)u.Count;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                h = (h ^ (ulong)i) * 1099511628211UL;
                h = (h ^ (ulong)math.asuint(u.Pos[i].x)) * 1099511628211UL;
                h = (h ^ (ulong)math.asuint(u.Pos[i].y)) * 1099511628211UL;
                h = (h ^ (ulong)math.asuint(u.Hp[i])) * 1099511628211UL;
                h = (h ^ u.Species[i]) * 1099511628211UL;
            }
            return h;
        }

        static List<int> BrushPowers(ContentDB db)
        {
            var list = new List<int>();
            for (int i = 0; i < db.Powers.Count; i++)
                if (PowerOps.IsSupported(db.Powers[i], db)) list.Add(i);
            return list;
        }

        static void EnqueueRandomEdit(PowerCommandQueue queue, List<int> brushes, WorldMap map, ref SimRandom rng, long tick)
        {
            var from = new int2(rng.Range(0, map.Width), rng.Range(0, map.Height));
            var to = math.clamp(from + new int2(rng.Range(-20, 21), rng.Range(-20, 21)), 0, new int2(map.Width - 1, map.Height - 1));
            queue.Enqueue(new PowerCommand
            {
                PowerId = (ushort)brushes[rng.Range(0, brushes.Count)],
                From = from,
                To = to,
                Brush = new BrushSpec(rng.Chance(0.5f) ? BrushShape.Circle : BrushShape.Square, Brushes.Radii[rng.Range(0, 6)]),
                Tick = tick,
            });
        }

        static int CountFeatures(WorldMap map)
        {
            int n = 0;
            for (int i = 0; i < map.TileCount; i++)
                if (map.Feature[i] != 0) n++;
            return n;
        }

        // Runs `years` of simulation without rendering and prints a short report.
        public static void Headless(ContentDB db, WorldGenSettings settings, int years)
        {
            var sw = Stopwatch.StartNew();
            var map = WorldGenerator.Generate(settings.Clone(), db);
            double genMs = sw.Elapsed.TotalMilliseconds;
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                if (settings.SpawnAnimals) sim.SpawnInitialAnimals();
                sw.Restart();
                long ticks = (long)years * SimConst.TicksPerYear;
                for (long t = 0; t < ticks; t++) sim.Tick();
                double runMs = sw.Elapsed.TotalMilliseconds;
                Debug.Log($"[Headless] seed {settings.Seed} map {map.Width}x{map.Height} gen {genMs:0} ms | {years} years = {ticks} ticks in {runMs:0} ms " +
                          $"({ticks / System.Math.Max(0.001, runMs / 1000.0):0} ticks/s) | population {sim.Population} | regions {sim.Regions.RegionCount} " +
                          $"islands {sim.Regions.IslandCount} | managed memory {System.GC.GetTotalMemory(false) / (1024 * 1024)} MB");
                var n = sim.Nature;
                Debug.Log($"[Headless] nature: era {n.CurrentEra.Id} (since year {n.Era.StartedYear}) | burning {n.Burning.Length} | lava {n.Lava.Length} " +
                          $"| clouds {n.Clouds.Length} | seeds {n.Seeds.Length} | features {CountFeatures(map)}");
            }
        }
    }
}
