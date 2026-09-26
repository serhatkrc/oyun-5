using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PG.Content
{
    public sealed class ContentLoadException : Exception
    {
        public ContentLoadException(IReadOnlyList<string> errors)
            : base("Content validation failed:\n" + string.Join("\n", errors))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }

    public sealed class ContentDB
    {
        public readonly Registry<TileTypeDef> Tiles = new Registry<TileTypeDef>("tiles");
        public readonly Registry<BiomeDef> Biomes = new Registry<BiomeDef>("biomes");
        public readonly Registry<FeatureDef> Features = new Registry<FeatureDef>("features");
        public readonly Registry<PowerDef> Powers = new Registry<PowerDef>("powers");
        public readonly Registry<WorldGenTemplateDef> WorldGenTemplates = new Registry<WorldGenTemplateDef>("worldgen_templates");
        public readonly Registry<BiomeTableDef> BiomeTables = new Registry<BiomeTableDef>("biome_table");
        public readonly Registry<UiStringDef> UiStrings = new Registry<UiStringDef>("ui_strings");
        public readonly Registry<CloudDef> Clouds = new Registry<CloudDef>("clouds");
        public readonly Registry<EraDef> Eras = new Registry<EraDef>("eras");
        public readonly Registry<DisasterDef> Disasters = new Registry<DisasterDef>("disasters");
        public readonly Registry<WorldLawDef> Laws = new Registry<WorldLawDef>("world_laws");
        public readonly Registry<SpeciesDef> Species = new Registry<SpeciesDef>("species");
        public readonly Registry<UnitTraitDef> UnitTraits = new Registry<UnitTraitDef>("unit_traits");
        public readonly Registry<StatusEffectDef> StatusEffects = new Registry<StatusEffectDef>("status_effects");
        public readonly Registry<SpellDef> Spells = new Registry<SpellDef>("spells");
        public readonly Registry<SubspeciesTraitDef> SubspeciesTraits = new Registry<SubspeciesTraitDef>("subspecies_traits");
        public readonly Registry<GeneDef> Genes = new Registry<GeneDef>("genes");
        public readonly Registry<GeneSynergyDef> GeneSynergies = new Registry<GeneSynergyDef>("gene_synergies");
        public readonly Registry<PhenotypeDef> Phenotypes = new Registry<PhenotypeDef>("phenotypes");
        public readonly Registry<EvolutionRuleDef> EvolutionRules = new Registry<EvolutionRuleDef>("evolution_rules");
        public readonly Registry<MetamorphosisDef> Metamorphoses = new Registry<MetamorphosisDef>("metamorphoses");
        public readonly Registry<DiseaseDef> Diseases = new Registry<DiseaseDef>("diseases");
        public readonly Registry<ResourceDef> Resources = new Registry<ResourceDef>("resources");
        public readonly Registry<BuildingDef> Buildings = new Registry<BuildingDef>("buildings");
        public readonly Registry<BuildingStyleDef> BuildingStyles = new Registry<BuildingStyleDef>("building_styles");
        public readonly Registry<JobDef> Jobs = new Registry<JobDef>("jobs");
        public readonly Registry<EquipmentTypeDef> EquipmentTypes = new Registry<EquipmentTypeDef>("equipment_types");
        public readonly Registry<MaterialDef> Materials = new Registry<MaterialDef>("materials");
        public readonly Registry<ItemQualityDef> ItemQualities = new Registry<ItemQualityDef>("item_qualities");
        public readonly Registry<HappinessEventDef> HappinessEvents = new Registry<HappinessEventDef>("happiness_events");
        public readonly Registry<KingdomTraitDef> KingdomTraits = new Registry<KingdomTraitDef>("kingdom_traits");
        public readonly Registry<WarTypeDef> WarTypes = new Registry<WarTypeDef>("war_types");
        public GeneRules GeneRules { get; private set; } = new GeneRules();
        public ZombieRules ZombieRules { get; private set; } = new ZombieRules();
        public NameSets NameSets { get; private set; } = new NameSets();
        // ... extended in later chapters

        // Leveled ground tiles indexed by level (0 = deep ocean ... MaxLevel = summit).
        public int[] TileByLevel { get; private set; } = Array.Empty<int>();
        public int MaxLevel => TileByLevel.Length - 1;

        public static ContentDB LoadAll(string basePath, IReadOnlyList<string> modPaths)
        {
            var db = new ContentDB();
            var errors = new List<string>();
            var serializer = JsonSerializer.CreateDefault();

            db.LoadFolder(basePath, true, serializer, errors);
            if (modPaths != null)
                for (int i = 0; i < modPaths.Count; i++)
                    db.LoadFolder(Path.Combine(modPaths[i], "Data"), false, serializer, errors);

            if (errors.Count == 0) db.Resolve(errors);
            if (errors.Count == 0) ContentValidator.Validate(db, errors);
            if (errors.Count > 0) throw new ContentLoadException(errors);
            return db;
        }

        void LoadFolder(string folder, bool required, JsonSerializer serializer, List<string> errors)
        {
            if (!Directory.Exists(folder))
            {
                if (required) errors.Add($"Content folder not found: {folder}");
                return;
            }
            LoadFile(Tiles, folder, required, serializer, errors);
            LoadFile(Biomes, folder, required, serializer, errors);
            LoadFile(Features, folder, required, serializer, errors);
            LoadFile(Powers, folder, required, serializer, errors);
            LoadFile(WorldGenTemplates, folder, required, serializer, errors);
            LoadFile(BiomeTables, folder, required, serializer, errors);
            LoadFile(UiStrings, folder, required, serializer, errors);
            LoadFile(Clouds, folder, required, serializer, errors);
            LoadFile(Eras, folder, required, serializer, errors);
            LoadFile(Disasters, folder, required, serializer, errors);
            LoadFile(Laws, folder, required, serializer, errors);
            LoadFile(Species, folder, required, serializer, errors);
            LoadFile(UnitTraits, folder, required, serializer, errors);
            LoadFile(StatusEffects, folder, required, serializer, errors);
            LoadFile(Spells, folder, required, serializer, errors);
            LoadFile(SubspeciesTraits, folder, required, serializer, errors);
            LoadFile(Genes, folder, required, serializer, errors);
            LoadFile(GeneSynergies, folder, required, serializer, errors);
            LoadFile(Phenotypes, folder, required, serializer, errors);
            LoadFile(EvolutionRules, folder, required, serializer, errors);
            LoadFile(Metamorphoses, folder, required, serializer, errors);
            LoadFile(Diseases, folder, required, serializer, errors);
            LoadFile(Resources, folder, required, serializer, errors);
            LoadFile(Buildings, folder, required, serializer, errors);
            LoadFile(BuildingStyles, folder, required, serializer, errors);
            LoadFile(Jobs, folder, required, serializer, errors);
            LoadFile(EquipmentTypes, folder, required, serializer, errors);
            LoadFile(Materials, folder, required, serializer, errors);
            LoadFile(ItemQualities, folder, required, serializer, errors);
            LoadFile(HappinessEvents, folder, required, serializer, errors);
            LoadFile(KingdomTraits, folder, required, serializer, errors);
            LoadFile(WarTypes, folder, required, serializer, errors);
            var geneRules = LoadObject(folder, "gene_rules", required, errors);
            if (geneRules != null) GeneRules = GeneRules.From(geneRules);
            var zombieRules = LoadObject(folder, "zombie_rules", required, errors);
            if (zombieRules != null) ZombieRules = ZombieRules.From(zombieRules);
            var nameSets = LoadObject(folder, "name_sets", required, errors);
            if (nameSets != null) NameSets = NameSets.From(nameSets);
        }

        // Single-object files (gene_rules, zombie_rules): a mod file replaces the base object.
        static JObject LoadObject(string folder, string name, bool required, List<string> errors)
        {
            string path = Path.Combine(folder, name + ".json");
            if (!File.Exists(path))
            {
                if (required) errors.Add($"Missing content file: {path}");
                return null;
            }
            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch (JsonException e)
            {
                errors.Add($"{path}: {e.Message}");
                return null;
            }
        }

        static void LoadFile<T>(Registry<T> registry, string folder, bool required, JsonSerializer serializer, List<string> errors)
            where T : ContentDef
        {
            string path = Path.Combine(folder, registry.Category + ".json");
            if (!File.Exists(path))
            {
                if (required) errors.Add($"Missing content file: {path}");
                return;
            }

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch (JsonException e)
            {
                errors.Add($"{path}: {e.Message}");
                return;
            }

            if (!(root["items"] is JArray items))
            {
                errors.Add($"{path}: missing 'items' array");
                return;
            }

            string source = Path.GetFileName(path);
            foreach (var token in items)
            {
                if (token is JObject obj) registry.AddOrPatch(obj, serializer, source, errors);
                else errors.Add($"{source}: non-object entry in items");
            }
        }

        void Resolve(List<string> errors)
        {
            ResolveAll(Tiles, errors);
            ResolveAll(Biomes, errors);
            ResolveAll(Features, errors);
            ResolveAll(Powers, errors);
            ResolveAll(WorldGenTemplates, errors);
            ResolveAll(BiomeTables, errors);
            ResolveAll(UiStrings, errors);
            ResolveAll(Clouds, errors);
            ResolveAll(Eras, errors);
            ResolveAll(Disasters, errors);
            ResolveAll(Laws, errors);
            ResolveAll(StatusEffects, errors);
            ResolveAll(SubspeciesTraits, errors);
            ResolveAll(Genes, errors);
            ResolveAll(GeneSynergies, errors);
            ResolveAll(Phenotypes, errors);
            ResolveAll(EvolutionRules, errors);
            ResolveAll(Diseases, errors);
            ResolveAll(Metamorphoses, errors);
            ResolveAll(Spells, errors);
            ResolveAll(UnitTraits, errors);
            ResolveAll(Species, errors);
            ResolveAll(Resources, errors);
            ResolveAll(Buildings, errors);
            for (int i = 0; i < Buildings.Count; i++) Buildings[i].ResolveRequirements(this, errors);
            ResolveAll(BuildingStyles, errors);
            ResolveAll(Jobs, errors);
            ResolveAll(EquipmentTypes, errors);
            ResolveAll(Materials, errors);
            ResolveAll(ItemQualities, errors);
            ResolveAll(HappinessEvents, errors);
            ResolveAll(KingdomTraits, errors);
            ResolveAll(WarTypes, errors);

            int maxLevel = -1;
            for (int i = 0; i < Tiles.Count; i++) maxLevel = Math.Max(maxLevel, Tiles[i].Level);
            var byLevel = new int[maxLevel + 1];
            for (int i = 0; i < byLevel.Length; i++) byLevel[i] = -1;
            for (int i = 0; i < Tiles.Count; i++)
            {
                var t = Tiles[i];
                if (!t.IsLeveled) continue;
                if (byLevel[t.Level] >= 0) errors.Add($"{t.Id}: level {t.Level} already used by {Tiles[byLevel[t.Level]].Id}");
                else byLevel[t.Level] = i;
            }
            for (int l = 0; l < byLevel.Length; l++)
                if (byLevel[l] < 0) errors.Add($"tiles: no tile for level {l}");
            TileByLevel = byLevel;
        }

        void ResolveAll<T>(Registry<T> registry, List<string> errors) where T : ContentDef
        {
            for (int i = 0; i < registry.Count; i++) registry[i].Resolve(this, errors);
        }

        public string Text(string key, string language)
        {
            return UiStrings.TryGet(key, out var s) ? s.Get(language) : key;
        }
    }
}
