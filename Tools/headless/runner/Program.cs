using System;
using System.IO;
using PG.Content;
using PG.WorldGen;
public static class Program
{
    public static int Main(string[] args)
    {
        HarnessData.Sync();
        var db = ContentDB.LoadAll(Path.Combine(UnityEngine.Application.streamingAssetsPath, "Data"), null);
        string cmd = args.Length > 0 ? args[0] : "determinism";
        switch (cmd)
        {
            case "determinism":
                return PG.Boot.Diagnostics.DeterminismCheck(db, new WorldGenSettings { Seed = 42, Size = MapSizePreset.Small }, args.Length > 1 ? int.Parse(args[1]) : 1000) ? 0 : 1;
            case "headless":
                PG.Boot.Diagnostics.Headless(db, new WorldGenSettings { Seed = 42, Size = MapSizePreset.Small, SpawnAnimals = true }, args.Length > 1 ? int.Parse(args[1]) : 5); return 0;
            case "soak": PG.EditorTools.NatureSoak.Run(); return UnityEditor.EditorApplication.ExitCode;
            case "units": PG.EditorTools.NatureSoak.RunUnits(); return UnityEditor.EditorApplication.ExitCode;
            case "feed": PG.EditorTools.NatureSoak.FeedDebug(); return 0;
            case "predprey": return PG.EditorTools.UnitAcceptance.PredatorPrey(db);
            case "starve": return PG.EditorTools.UnitAcceptance.Starvation(db);
            case "unitdet": return PG.EditorTools.UnitAcceptance.Determinism(db);
            case "acceptance": PG.EditorTools.UnitAcceptance.Run(); return UnityEditor.EditorApplication.ExitCode;
            case "wolf": WolfDebug.Run(db, args.Length > 1 ? ulong.Parse(args[1]) : 11); return 0;
            case "civ": PG.EditorTools.CivSoak.Run(); return UnityEditor.EditorApplication.ExitCode;
            case "village": return PG.EditorTools.CivSoak.Village(db, args.Length > 1 ? int.Parse(args[1]) : 100);
            case "barren": return PG.EditorTools.CivSoak.Barren(db);
            case "placement": return PG.EditorTools.CivSoak.Placement(db);
            case "flood": return PG.EditorTools.CivSoak.Flood(db);
            case "quality": return PG.EditorTools.CivSoak.Quality(db);
            case "civsave": return PG.EditorTools.CivSoak.SaveLoad(db);
            case "civperf": return PG.EditorTools.CivSoak.Performance(db);
            case "civdebug": CivDebug.Run(db, args.Length > 1 ? int.Parse(args[1]) : 12); return 0;
            case "meta": PG.EditorTools.MetaSoak.Run(); return UnityEditor.EditorApplication.ExitCode;
            case "metaworld": return PG.EditorTools.MetaSoak.World(db, args.Length > 1 ? int.Parse(args[1]) : 200, args.Length > 2 ? ulong.Parse(args[2]) : 31);
            case "loyalty": return PG.EditorTools.MetaSoak.Loyalty();
            case "succession": return PG.EditorTools.MetaSoak.Succession(db);
            case "conquest": return PG.EditorTools.MetaSoak.Conquest(db);
            case "metaperf": return PG.EditorTools.MetaSoak.Performance(db);
            case "colony": return PG.EditorTools.MetaSoak.Colony(db);
            case "metasave": return PG.EditorTools.MetaSoak.SaveLoad(db);
            case "metadebug": MetaDebug.Run(db, args.Length > 1 ? int.Parse(args[1]) : 120, args.Length > 2 ? ulong.Parse(args[2]) : 31); return 0;
            case "colonydebug": ColonyDebug.Run(db); return 0;
            case "temp": TempDebug.Run(db); return 0;
            case "profile": Profile.Run(db, args.Length > 1 ? int.Parse(args[1]) : 5000, args.Length > 2 ? int.Parse(args[2]) : 600, MapSizePreset.Huge); return 0;
        }
        Console.WriteLine("unknown command"); return 2;
    }
}
