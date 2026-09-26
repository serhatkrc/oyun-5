using System.Collections.Generic;

namespace PG.Content
{
    // Cross-reference and range checks run after loading. All problems are collected into one list.
    public static class ContentValidator
    {
        public static void Validate(ContentDB db, List<string> errors)
        {
            if (db.Tiles.Count == 0 || db.Tiles.Count > 255) errors.Add($"tiles: count {db.Tiles.Count} must be 1..255 (stored as byte)");
            if (db.Biomes.Count > 254) errors.Add($"biomes: count {db.Biomes.Count} must be <= 254 (0 = no biome)");
            if (db.Features.Count > 65534) errors.Add("features: too many records");
            if (db.WorldGenTemplates.Count == 0) errors.Add("worldgen_templates: at least one template required");
            if (db.BiomeTables.Count == 0) errors.Add("biome_table: at least one table required");

            for (int i = 0; i < db.Tiles.Count; i++)
            {
                var t = db.Tiles[i];
                if (t.ReliefShade < 0f || t.ReliefShade > 0.5f) errors.Add($"{t.Id}: reliefShade {t.ReliefShade} out of range 0..0.5");
                if (t.Walkable && t.MoveCost <= 0f) errors.Add($"{t.Id}: walkable tile needs moveCost > 0");
                if (t.RenderMaterial != null && t.RenderMaterial != "" && t.RenderMaterial != "water" && t.RenderMaterial != "lava")
                    errors.Add($"{t.Id}: unknown renderMaterial '{t.RenderMaterial}'");
            }

            for (int i = 0; i < db.Biomes.Count; i++)
            {
                var b = db.Biomes[i];
                if (b.Temp < 0f || b.Temp > 1f || b.Moisture < 0f || b.Moisture > 1f) errors.Add($"{b.Id}: temp/moisture must be 0..1");
            }

            if (db.UnitTraits.Count > 256) errors.Add($"unit_traits: count {db.UnitTraits.Count} must be <= 256 (TraitSet bits)");
            if (db.Species.Count > ushort.MaxValue) errors.Add("species: too many records");
            if (db.SubspeciesTraits.Count > 256) errors.Add($"subspecies_traits: count {db.SubspeciesTraits.Count} must be <= 256 (TraitSet bits)");
            if (db.Genes.Count >= ushort.MaxValue) errors.Add("genes: too many records");

            for (int i = 0; i < db.Features.Count; i++)
                if (db.Features[i].VeinWeight < 0f) errors.Add($"{db.Features[i].Id}: veinWeight must be >= 0");

            // Only Faz 1 target ops are checked; other ops (zone_add, freeze, ...) belong to later chapters.
            for (int i = 0; i < db.Powers.Count; i++)
            {
                var p = db.Powers[i];
                if (p.Type != "brush") continue;
                string op = p.ParamString("op");
                if (op == "set" || op == "raise_to" || op == "lower_to")
                {
                    string target = p.ParamString("target");
                    if (!db.Tiles.TryGet("tile." + target, out var tile)) errors.Add($"{p.Id}: unknown target tile '{target}'");
                    else if (op != "set" && !tile.IsLeveled) errors.Add($"{p.Id}: {op} target '{target}' has no level");
                }
            }
        }
    }
}
