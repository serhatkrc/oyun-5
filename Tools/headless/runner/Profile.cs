using System;
using System.Collections.Generic;
using System.Diagnostics;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.WorldGen;
using Unity.Mathematics;

// Per-system timing with many units (harness only; JIT timings, not Burst).
public static class Profile
{
    public static void Run(ContentDB db, int units, int ticks, MapSizePreset size)
    {
        var settings = new WorldGenSettings { Seed = 77, Size = size };
        var map = WorldGenerator.Generate(settings, db);
        using var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed));
        string[] kinds = { "sp.sheep", "sp.cow", "sp.rabbit", "sp.deer", "sp.wolf", "sp.bear", "sp.fox", "sp.boar" };
        var rng = new SimRandom(5, 5);
        for (int k = 0; k < kinds.Length; k++)
        {
            int species = db.Species.IdOf(kinds[k]);
            for (int n = 0, tries = 0; n < units / kinds.Length && tries < 100000; tries++)
            {
                int x = rng.Range(0, map.Width), y = rng.Range(0, map.Height);
                if (!map.IsWalkable(x, y) || map.IsWater(x, y)) continue;
                float life = db.Species[species].BaseStats[(int)StatId.Lifespan];
                sim.Units.Store.Spawn(new UnitSpawnRequest { Species = species, Pos = new float2(x + 0.5f, y + 0.5f), AgeYears = life * rng.Range(0.2f, 0.6f), Sex = n % 2, Mother = EntityId.None, Father = EntityId.None, Subspecies = -1 }, 0, ref rng);
                n++;
            }
        }
        Console.WriteLine($"[Profile] spawned {sim.Units.Store.Count}");
        for (int t = 0; t < 200; t++) sim.Tick();
        Console.WriteLine($"[Profile] after warmup {sim.Units.Store.Count}");
        var sums = new double[sim.Pipeline.Count];
        var sw = Stopwatch.StartNew();
        for (int t = 0; t < ticks; t++)
        {
            sim.Tick();
            for (int s = 0; s < sums.Length; s++) sums[s] += sim.Pipeline.LastMs(s);
        }
        var ps = sim.Units.Paths; Console.WriteLine($"[Profile] paths: searches {ps.StatSearches} failed {ps.StatFailed} straight {ps.StatStraight} detour {ps.StatDetour} avg expansions {(ps.StatSearches > 0 ? ps.StatExpansions / ps.StatSearches : 0)}");
        Console.WriteLine($"[Profile] {sim.Units.Store.Count} units, {ticks} ticks, {sw.Elapsed.TotalMilliseconds / ticks:0.00} ms/tick total");
        var order = new List<int>(); for (int s = 0; s < sums.Length; s++) order.Add(s);
        order.Sort((a, b) => sums[b].CompareTo(sums[a]));
        foreach (int s in order) if (sums[s] / ticks > 0.01) Console.WriteLine($"  {sim.Pipeline[s].GetType().Name,-28} {sums[s] / ticks,7:0.000} ms");
    }
}
