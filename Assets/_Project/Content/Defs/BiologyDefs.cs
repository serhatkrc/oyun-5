using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PG.Content
{
    // Bölüm 4.1: capabilities granted by subspecies traits (flag:x); Bölüm 5-6 read them.
    [Flags]
    public enum AbilityFlags : ushort
    {
        None = 0,
        Sapient = 1 << 0,
        CanUseItems = 1 << 1,
        CanBuild = 1 << 2,
        CanHoldCulture = 1 << 3,
        CanSpeak = 1 << 4,
        CanHoldLanguage = 1 << 5,
        CanHoldReligion = 1 << 6,
        CanPlot = 1 << 7,
    }

    public static class AbilityKeys
    {
        public static AbilityFlags Parse(string name)
        {
            switch (name)
            {
                case "sapient": return AbilityFlags.Sapient;
                case "can_use_items": return AbilityFlags.CanUseItems;
                case "can_build": return AbilityFlags.CanBuild;
                case "can_hold_culture": return AbilityFlags.CanHoldCulture;
                case "can_speak": return AbilityFlags.CanSpeak;
                case "can_hold_language": return AbilityFlags.CanHoldLanguage;
                case "can_hold_religion": return AbilityFlags.CanHoldReligion;
                case "can_plot": return AbilityFlags.CanPlot;
                default: return AbilityFlags.None;
            }
        }

        internal static string[] Strings(JObject e, string key)
        {
            if (!(e?[key] is JArray arr)) return Array.Empty<string>();
            var result = new string[arr.Count];
            for (int i = 0; i < arr.Count; i++) result[i] = arr[i].Value<string>();
            return result;
        }
    }

    // subspecies_traits.json
    public sealed class SubspeciesTraitDef : ContentDef
    {
        public string Group { get; set; }
        public JObject Effects { get; set; }
        public bool CanAppearRandomly { get; set; }

        [JsonIgnore] public StatMod[] Mods { get; private set; }
        [JsonIgnore] public Dictionary<string, float> Behavior { get; private set; }
        [JsonIgnore] public UnitFlags Flags { get; private set; }
        [JsonIgnore] public AbilityFlags Abilities { get; private set; }
        [JsonIgnore] public string[] Immunes { get; private set; }
        [JsonIgnore] public string[] Neurons { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var e = Effects ?? new JObject();
            Behavior = new Dictionary<string, float>();
            Mods = StatKeys.Parse(e["add"] as JObject, e["pct"] as JObject, Behavior);
            var flags = UnitFlags.None;
            var abilities = AbilityFlags.None;
            foreach (var f in AbilityKeys.Strings(e, "flags"))
            {
                flags |= StatKeys.Flag(f);
                abilities |= AbilityKeys.Parse(f);
            }
            Flags = flags;
            Abilities = abilities;
            Immunes = AbilityKeys.Strings(e, "immunes");
            Neurons = AbilityKeys.Strings(e, "neurons");
        }
    }

    // genes.json
    public sealed class GeneDef : ContentDef
    {
        public JObject Effects { get; set; }
        [JsonIgnore] public StatMod[] Mods { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var e = Effects ?? new JObject();
            Mods = StatKeys.Parse(e["add"] as JObject, e["pct"] as JObject, null);
        }
    }

    // gene_synergies.json: required genes side by side on one chromosome (any order) unlock the bonus.
    public sealed class GeneSynergyDef : ContentDef
    {
        public string[] Requires { get; set; }
        public JObject Bonus { get; set; }
        [JsonIgnore] public int[] RequiredGenes { get; private set; }
        [JsonIgnore] public StatMod[] Mods { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var list = new List<int>();
            foreach (var g in Requires ?? Array.Empty<string>())
            {
                int id = db.Genes.IdOrDefault(g);
                if (id >= 0) list.Add(id);
                else errors.Add($"{Id}: unknown gene '{g}'");
            }
            RequiredGenes = list.ToArray();
            Mods = StatKeys.Parse(Bonus?["add"] as JObject, Bonus?["pct"] as JObject, null);
        }
    }

    // phenotypes.json
    public sealed class PhenotypeDef : ContentDef
    {
        public string BaseColor { get; set; }
        public string Pattern { get; set; }
        public string[] BiomeBias { get; set; }
        [JsonIgnore] public Color32 Color { get; private set; }
        [JsonIgnore] public int[] BiasBiomes { get; private set; } // biome indices

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (!HexColor.TryParse(BaseColor, out var c)) errors.Add($"{Id}: invalid baseColor '{BaseColor}'");
            Color = c;
            var list = new List<int>();
            foreach (var b in BiomeBias ?? Array.Empty<string>())
            {
                int id = db.Biomes.IdOrDefault(b);
                if (id >= 0) list.Add(id);
            }
            BiasBiomes = list.ToArray();
        }
    }

    // evolution_rules.json (monolith stages)
    public sealed class EvolutionRuleDef : ContentDef
    {
        public int Stage { get; set; }
        public string[] Grants { get; set; }
        public string Note { get; set; }
        [JsonIgnore] public int[] GrantTraits { get; private set; } // SubspeciesTraitDef indices

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var list = new List<int>();
            foreach (var g in Grants ?? Array.Empty<string>())
            {
                int id = db.SubspeciesTraits.IdOrDefault(g);
                if (id >= 0) list.Add(id);
                else errors.Add($"{Id}: unknown subspecies trait '{g}'");
            }
            GrantTraits = list.ToArray();
        }
    }

    public enum MetaTrigger : byte { Age, Disease, Era, Merge }
    public enum MetaFilter : byte { Species, Any, Animal, SizeAtLeast, Feature }

    // metamorphoses.json: from (species | * | *animal | *size>=n | feat.x), to, trigger (age:x | dis_x[:%p] | era.x | merge:n)
    public sealed class MetamorphosisDef : ContentDef
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Trigger { get; set; }
        public string Note { get; set; }

        [JsonIgnore] public MetaFilter Filter { get; private set; }
        [JsonIgnore] public int FromSpecies { get; private set; } = -1;
        [JsonIgnore] public ushort FromFeature { get; private set; }
        [JsonIgnore] public float FilterSize { get; private set; }
        [JsonIgnore] public int ToSpecies { get; private set; } = -1;
        [JsonIgnore] public MetaTrigger TriggerKind { get; private set; }
        [JsonIgnore] public float TriggerValue { get; private set; }   // years for age, n for merge
        [JsonIgnore] public int TriggerDisease { get; private set; } = -1;
        [JsonIgnore] public int TriggerEra { get; private set; } = -1;
        [JsonIgnore] public float Chance { get; private set; } = 1f;   // dis_x:%25

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            string from = From ?? "*";
            if (from == "*") Filter = MetaFilter.Any;
            else if (from == "*animal") Filter = MetaFilter.Animal;
            else if (from.StartsWith("*size>="))
            {
                Filter = MetaFilter.SizeAtLeast;
                FilterSize = float.Parse(from.Substring(7), CultureInfo.InvariantCulture);
            }
            else if (from.StartsWith("feat."))
            {
                Filter = MetaFilter.Feature;
                if (db.Features.TryGet(from, out var f)) FromFeature = f.MapValue;
                else errors.Add($"{Id}: unknown feature '{from}'");
            }
            else
            {
                Filter = MetaFilter.Species;
                FromSpecies = db.Species.IdOrDefault(from);
                if (FromSpecies < 0) errors.Add($"{Id}: unknown species '{from}'");
            }
            ToSpecies = db.Species.IdOrDefault(To);
            if (ToSpecies < 0) errors.Add($"{Id}: unknown target species '{To}'");

            string t = Trigger ?? "";
            if (t.StartsWith("age:"))
            {
                TriggerKind = MetaTrigger.Age;
                TriggerValue = float.Parse(t.Substring(4), CultureInfo.InvariantCulture);
            }
            else if (t.StartsWith("merge:"))
            {
                TriggerKind = MetaTrigger.Merge;
                TriggerValue = float.Parse(t.Substring(6), CultureInfo.InvariantCulture);
            }
            else if (t.StartsWith("era."))
            {
                TriggerKind = MetaTrigger.Era;
                TriggerEra = db.Eras.IdOrDefault(t);
                if (TriggerEra < 0) errors.Add($"{Id}: unknown era '{t}'");
            }
            else if (t.StartsWith("dis_"))
            {
                TriggerKind = MetaTrigger.Disease;
                var parts = t.Split(':');
                TriggerDisease = db.Diseases.IdOrDefault(parts[0]);
                if (TriggerDisease < 0) errors.Add($"{Id}: unknown disease '{parts[0]}'");
                if (parts.Length > 1) Chance = float.Parse(parts[1].TrimStart('%'), CultureInfo.InvariantCulture) / 100f;
            }
            else errors.Add($"{Id}: unknown trigger '{t}'");
        }
    }

    [Flags]
    public enum SpreadFlags : byte { None = 0, Contact = 1, Bite = 2, Rat = 4, Cloud = 8, Air = 16, Mosquito = 32, Magic = 64 }

    // diseases.json
    public sealed class DiseaseDef : ContentDef
    {
        public string[] Spread { get; set; }
        public float Contagion { get; set; }
        public float Lethality { get; set; }
        public int DurationMonths { get; set; }
        public string TransformsInto { get; set; }
        public string Note { get; set; }

        [JsonIgnore] public SpreadFlags SpreadMask { get; private set; }
        [JsonIgnore] public int TransformSpecies { get; private set; } = -1;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var mask = SpreadFlags.None;
            foreach (var s in Spread ?? Array.Empty<string>())
            {
                if (Enum.TryParse(s, true, out SpreadFlags f)) mask |= f;
                else errors.Add($"{Id}: unknown spread '{s}'");
            }
            SpreadMask = mask;
            if (!string.IsNullOrEmpty(TransformsInto))
            {
                TransformSpecies = db.Species.IdOrDefault(TransformsInto);
                if (TransformSpecies < 0) errors.Add($"{Id}: unknown transformsInto '{TransformsInto}'");
            }
            if (DurationMonths <= 0) DurationMonths = 1;
        }
    }

    // gene_rules.json (single object)
    public sealed class GeneRules
    {
        public int ChromosomeCount = 2, ChromosomeMax = 4, SlotsPerChromosome = 6, SlotsMax = 10;
        public float MutationPerBirth = 0.02f;
        public string[] MutationOps = Array.Empty<string>();

        public static GeneRules From(JObject o)
        {
            var r = new GeneRules();
            if (o == null) return r;
            r.ChromosomeCount = o["chromosomeCount"]?.Value<int?>("default") ?? r.ChromosomeCount;
            r.ChromosomeMax = o["chromosomeCount"]?.Value<int?>("max") ?? r.ChromosomeMax;
            r.SlotsPerChromosome = o["slotsPerChromosome"]?.Value<int?>("default") ?? r.SlotsPerChromosome;
            r.SlotsMax = o["slotsPerChromosome"]?.Value<int?>("max") ?? r.SlotsMax;
            r.MutationPerBirth = o.Value<float?>("mutationPerBirth") ?? r.MutationPerBirth;
            r.MutationOps = AbilityKeys.Strings(o, "mutationOps");
            return r;
        }
    }

    // zombie_rules.json (single object; the numeric parts the simulation reads)
    public sealed class ZombieRules
    {
        public float BitePerHit = 0.35f, BiteOnKill = 0.9f;
        public int TurningMonths = 2, RiseAfterTicks = 40;

        public static ZombieRules From(JObject o)
        {
            var r = new ZombieRules();
            var inf = o?["infection"] as JObject;
            if (inf == null) return r;
            r.BitePerHit = inf.Value<float?>("bitePerHit") ?? r.BitePerHit;
            r.BiteOnKill = inf.Value<float?>("biteOnKill") ?? r.BiteOnKill;
            r.TurningMonths = inf.Value<int?>("turningMonths") ?? r.TurningMonths;
            string rise = inf.Value<string>("onDeathWhileInfected");
            int colon = rise?.LastIndexOf(':') ?? -1;
            if (colon >= 0 && int.TryParse(rise.Substring(colon + 1), out int ticks)) r.RiseAfterTicks = ticks;
            return r;
        }
    }
}
