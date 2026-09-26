using System.Collections.Generic;
using Newtonsoft.Json;

namespace PG.Content
{
    // (temperature row, moisture column) -> biome grid used by worldgen step 7.
    public sealed class BiomeTableDef : ContentDef
    {
        public string[][] Grid { get; set; }
        public string[] Patches { get; set; }
        public int[] PatchSize { get; set; } = { 150, 800 };
        public float PatchesPerChunk { get; set; } = 0.4f;

        [JsonIgnore] public int Rows { get; private set; }
        [JsonIgnore] public int Cols { get; private set; }
        [JsonIgnore] public int[] GridIds { get; private set; }  // row-major biome indices
        [JsonIgnore] public int[] PatchIds { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            Rows = Grid?.Length ?? 0;
            Cols = Rows > 0 ? Grid[0].Length : 0;
            if (Rows == 0 || Cols == 0)
            {
                errors.Add($"{Id}: empty grid");
                GridIds = System.Array.Empty<int>();
            }
            else
            {
                GridIds = new int[Rows * Cols];
                for (int r = 0; r < Rows; r++)
                {
                    if (Grid[r].Length != Cols) errors.Add($"{Id}: grid row {r} has {Grid[r].Length} columns, expected {Cols}");
                    for (int c = 0; c < Cols && c < Grid[r].Length; c++)
                        GridIds[r * Cols + c] = Biome(db, Grid[r][c], errors);
                }
            }

            var patches = Patches ?? System.Array.Empty<string>();
            PatchIds = new int[patches.Length];
            for (int i = 0; i < patches.Length; i++) PatchIds[i] = Biome(db, patches[i], errors);
            if (PatchSize == null || PatchSize.Length != 2 || PatchSize[0] < 1 || PatchSize[1] < PatchSize[0])
                errors.Add($"{Id}: patchSize must be [min, max]");
        }

        int Biome(ContentDB db, string key, List<string> errors)
        {
            if (db.Biomes.TryGet(key, out var b)) return b.Index;
            errors.Add($"{Id}: unknown biome '{key}'");
            return 0;
        }
    }
}
