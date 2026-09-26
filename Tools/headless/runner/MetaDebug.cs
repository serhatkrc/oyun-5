using System;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.WorldGen;
using Unity.Mathematics;

public static class MetaDebug
{
    // Four species world; prints every army state change and war outcome.
    public static void Run(ContentDB db, int years, ulong seed)
    {
        var settings = new WorldGenSettings { Seed = seed, Size = MapSizePreset.Medium, Template = "wgt.pangea", ForestDensity = 0.4f };
        var map = WorldGenerator.Generate(settings, db);
        using var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed), null, "Meta");
        sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
        sim.SpawnInitialAnimals();
        string[] sp = { "sp.human", "sp.elf", "sp.dwarf", "sp.orc" };
        for (int s = 0; s < 4; s++)
        {
            int2 at = new int2(map.Width * (1 + 2 * (s % 2)) / 4, map.Height * (1 + 2 * (s / 2)) / 4);
            PG.EditorTools.CivSoak.SpawnGroup(sim, db.Species.IdOf(sp[s]), 12, PG.EditorTools.CivSoak.Meadow(map, at), 6, 11 + (ulong)s);
        }
        Console.WriteLine($"map {map.Width}x{map.Height}");
        var meta = sim.Meta;
        var states = new System.Collections.Generic.List<ArmyState>();
        sim.Events.Subscribe<WarDeclaredEvent>(e => Console.WriteLine($"y{sim.Clock.Year} WAR {e.War}: {meta.Kingdoms[e.Attacker].Name} -> {meta.Kingdoms[e.Defender].Name}"));
        sim.Events.Subscribe<WarEndedEvent>(e => { var w = meta.Wars[e.War]; Console.WriteLine($"y{sim.Clock.Year} END {e.War} {e.Result} casualties {w.CasualtiesA}/{w.CasualtiesD} captured {w.CapturedCities.Count}"); });
        sim.Events.Subscribe<CityCapturedEvent>(e => Console.WriteLine($"y{sim.Clock.Year} CAPTURED {sim.Civ.Cities[e.City].Name}"));
        sim.Events.Subscribe<RebellionEvent>(e => Console.WriteLine($"y{sim.Clock.Year} REBELLION {sim.Civ.Cities[e.City].Name}"));
        for (int t = 0; t < years * SimConst.TicksPerYear; t++)
        {
            sim.Tick();
            if (t % 20 != 8) continue;
            for (int a = 0; a < meta.Armies.Count; a++)
            {
                var army = meta.Armies[a];
                if (states.Count <= a) states.Add((ArmyState)255);
                if (states[a] == army.State) continue;
                states[a] = army.State;
                var target = army.TargetCity >= 0 ? sim.Civ.Cities[army.TargetCity] : null;
                float dist = 0f; int near = 0;
                if (target != null && army.Soldiers.Count > 0)
                {
                    foreach (int i in army.Soldiers) { float d = math.distance(sim.Units.Store.Pos[i], (float2)target.Center); dist += d; if (d < 14) near++; }
                    dist /= army.Soldiers.Count;
                }
                int defenders = target != null && !target.Dead && target.Kingdom >= 0 ? ArmySystem.Defenders(meta, target, 10f) : -1;
                Console.WriteLine($"y{sim.Clock.Year} m{sim.Clock.Month} army {a} ({meta.Kingdoms[army.Kingdom].Name}) -> {army.State} soldiers {army.Soldiers.Count}/{army.StartSize} target {target?.Name} avgDist {dist:0} near {near} defenders {defenders}");
            }
        }
        foreach (var c in sim.Civ.Cities) if (!c.Dead) Console.WriteLine($"{c.Name} k{c.Kingdom} pop {c.Population} loy {c.Loyalty} hap {c.Happiness} center {c.Center}");
    }
}

public static class ColonyDebug
{
    public static void Run(ContentDB db)
    {
      foreach (var tpl in new[] { "wgt.archipelago", "wgt.continents" })
      foreach (ulong seed in new ulong[] { 61, 62, 63 })
      {
        var settings = new WorldGenSettings { Seed = seed, Size = MapSizePreset.Medium, Template = tpl, ForestDensity = 0.3f };
        var map = WorldGenerator.Generate(settings, db);
        using var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed), null, "Colony");
        Console.Write(tpl + " " + seed + ": ");
        var paths = sim.Units.Paths;
        int2 c = PG.EditorTools.CivSoak.Meadow(map, new int2(map.Width / 2, map.Height / 2));
        Console.WriteLine($"meadow {c}");
        int2 port = new int2(1, 1);
        int land = 0, other = 0, unowned = 0, score = 0, shore = 0;
        var rng = new SimRandom(5, 5);
        for (int s = 0; s < 2000; s++)
        {
            int2 p = new int2(rng.Range(0, map.Width), rng.Range(0, map.Height));
            if (map.IsWater(p.x, p.y) || !map.IsWalkable(p.x, p.y) || !map.HasFlag(p.x, p.y, PG.Content.TileFlags.Buildable)) continue;
            land++;
            if (paths.SameIsland(c, p, Mobility.Land)) continue;
            other++;
            int z = map.ZoneIndexOf(p.x, p.y);
            if (map.Zones[z].OwnerCity >= 0) continue;
            unowned++;
            if (CityPlanner.ZoneScore(sim.Civ, z, true) <= 0f) continue;
            score++;
            bool ok = false;
            for (int dy = -6; dy <= 6 && !ok; dy++) for (int dx = -6; dx <= 6 && !ok; dx++)
            { int x = p.x + dx, y = p.y + dy; if (map.InBounds(x, y) && map.IsWater(x, y) && paths.SameIsland(new int2(x, y), port, Mobility.Water)) ok = true; }
            if (ok) shore++;
        }
        Console.WriteLine($"land {land} otherIsland {other} unowned {unowned} score {score} shore {shore}; port water {map.IsWater(port.x, port.y)}");
      }
    }
}
