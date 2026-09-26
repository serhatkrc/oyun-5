using System;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.WorldGen;
public static class TempDebug
{
    public static void Run(ContentDB db)
    {
        var settings = new WorldGenSettings { Seed = 17, Size = MapSizePreset.Medium, Template = "wgt.flat_green", ForestDensity = 0.4f };
        var map = WorldGenerator.Generate(settings, db);
        using var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed));
        int burningMax = 0;
        for (int m = 0; m < 36; m++)
        {
            for (int t = 0; t < 60; t++) { sim.Tick(); burningMax = Math.Max(burningMax, sim.Nature.Burning.Length); }
            int min = 999, max = -999, over35 = 0; long sum = 0;
            for (int z = 0; z < map.Zones.Length; z++) { int tc = map.Zones[z].TemperatureC; min = Math.Min(min, tc); max = Math.Max(max, tc); sum += tc; if (tc > 35) over35++; }
            int cz = map.ZoneIndexOf(map.Width / 2, map.Height / 2);
            Console.WriteLine($"month {m} temp min {min} max {max} avg {sum / map.Zones.Length} over35 zones {over35}/{map.Zones.Length} center {map.Zones[cz].TemperatureC} burningMax {burningMax}");
        }
    }
}
