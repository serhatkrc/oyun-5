using System.Collections.Generic;
using Newtonsoft.Json;

namespace PG.Content
{
    public sealed class WorldGenTemplateDef : ContentDef
    {
        public static readonly string[] MaskTypes = { "none", "radial", "multiBlob", "annulus", "inverse", "full", "empty", "image" };

        public sealed class MaskDef
        {
            public string Type { get; set; } = "none";
            public int[] Count { get; set; } = { 1, 1 };
            public float[] Radius { get; set; } = { 0.4f, 0.4f };
            public float FalloffPower { get; set; } = 2f;
            public float Width { get; set; } = 0.12f;
        }

        public sealed class NoiseDef
        {
            public int Octaves { get; set; } = 6;
            public float FrequencyMul { get; set; } = 1f;
            public float Lacunarity { get; set; } = 2f;
            public float Gain { get; set; } = 0.5f;
        }

        public sealed class WarpDef
        {
            public float Amplitude { get; set; } = 0.08f;
            public float FrequencyMul { get; set; } = 2f;
        }

        public MaskDef Mask { get; set; } = new MaskDef();
        public NoiseDef Noise { get; set; } = new NoiseDef();
        public WarpDef Warp { get; set; } = new WarpDef();
        public float EdgeOcean { get; set; } = 0.06f;
        public float? LandRatioDefault { get; set; }
        public string FlatTile { get; set; }   // "full"/"empty" masks: tile that fills the map
        public string FlatBiome { get; set; }

        [JsonIgnore] public int FlatTileId { get; private set; } = -1;
        [JsonIgnore] public int FlatBiomeId { get; private set; } = -1;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (System.Array.IndexOf(MaskTypes, Mask?.Type) < 0) errors.Add($"{Id}: unknown mask type '{Mask?.Type}'");
            if (!string.IsNullOrEmpty(FlatTile))
            {
                if (db.Tiles.TryGet(FlatTile, out var t)) FlatTileId = t.Index;
                else errors.Add($"{Id}.flatTile: unknown tile '{FlatTile}'");
            }
            if (!string.IsNullOrEmpty(FlatBiome))
            {
                if (db.Biomes.TryGet(FlatBiome, out var b)) FlatBiomeId = b.Index;
                else errors.Add($"{Id}.flatBiome: unknown biome '{FlatBiome}'");
            }
            if ((Mask?.Type == "full" || Mask?.Type == "empty") && FlatTileId < 0)
                errors.Add($"{Id}: mask '{Mask.Type}' needs flatTile");
        }
    }
}
