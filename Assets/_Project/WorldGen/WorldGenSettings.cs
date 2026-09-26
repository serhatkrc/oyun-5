using System;
using PG.Content;

namespace PG.WorldGen
{
    public enum MapSizePreset { Tiny, Small, Medium, Large, Huge, Giant, Titanic }

    public static class MapSizes
    {
        static readonly int[] Tiles = { 128, 192, 256, 384, 512, 768, 1024 };
        static readonly string[] Keys = { "ui.size.tiny", "ui.size.small", "ui.size.medium", "ui.size.large", "ui.size.huge", "ui.size.giant", "ui.size.titanic" };

        public static int Count => Tiles.Length;
        public static int TilesFor(MapSizePreset preset) => Tiles[(int)preset];
        public static string LocKey(MapSizePreset preset) => Keys[(int)preset];
    }

    [Serializable]
    public sealed class WorldGenSettings
    {
        public ulong Seed;                          // 0 = random (resolved by the generator)
        public MapSizePreset Size = MapSizePreset.Medium;
        public string Template = "wgt.continents";  // worldgen_templates.json
        public float LandRatio = 0.45f;             // 0.10-0.85 land / total target
        public float MountainRatio = 0.08f;         // 0.00-0.25 share of land that is mountain
        public float HillRatio = 0.12f;             // 0.00-0.30
        public float Roughness = 0.50f;             // 0-1 coastline warp strength
        public float ForestDensity = 0.50f;         // 0-1 (Bölüm 2)
        public float BiomeVariety = 0.50f;          // 0-1 how many special biome patches
        public float OreDensity = 0.50f;            // 0-1
        public float Temperature = 0.50f;           // 0 cold - 1 hot (global shift)
        public float Moisture = 0.50f;
        public bool SpawnAnimals = true;            // filled in Bölüm 3
        public bool SpawnCivs = false;
        public string ImagePath;                    // wgt.custom_image source PNG
        public int PreviewDivisor = 1;              // 4 = quarter-resolution preview

        public const float DefaultLandRatio = 0.45f;

        public WorldGenSettings Clone() => (WorldGenSettings)MemberwiseClone();

        // Selecting a template resets the land ratio to its default (archipelago 0.30, lakes 0.70).
        public void ApplyTemplateDefaults(WorldGenTemplateDef template)
        {
            Template = template.Id;
            LandRatio = template.LandRatioDefault ?? DefaultLandRatio;
        }

        public void Clamp()
        {
            LandRatio = Math.Clamp(LandRatio, 0.10f, 0.85f);
            MountainRatio = Math.Clamp(MountainRatio, 0f, 0.25f);
            HillRatio = Math.Clamp(HillRatio, 0f, 0.30f);
            Roughness = Math.Clamp(Roughness, 0f, 1f);
            ForestDensity = Math.Clamp(ForestDensity, 0f, 1f);
            BiomeVariety = Math.Clamp(BiomeVariety, 0f, 1f);
            OreDensity = Math.Clamp(OreDensity, 0f, 1f);
            Temperature = Math.Clamp(Temperature, 0f, 1f);
            Moisture = Math.Clamp(Moisture, 0f, 1f);
            if (PreviewDivisor < 1) PreviewDivisor = 1;
        }
    }
}
