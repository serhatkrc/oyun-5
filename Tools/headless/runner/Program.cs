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
            case "profile": Profile.Run(db, args.Length > 1 ? int.Parse(args[1]) : 5000, args.Length > 2 ? int.Parse(args[2]) : 600, MapSizePreset.Huge); return 0;
        }
        Console.WriteLine("unknown command"); return 2;
    }
}
