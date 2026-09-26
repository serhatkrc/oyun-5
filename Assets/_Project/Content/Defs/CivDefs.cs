using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace PG.Content
{
    // Bölüm 5 content: resources, buildings, styles, jobs, equipment, materials, item qualities, happiness events.

    public enum ResourceKind : byte { Material, Ore, Currency, Magic, Food, Medicine, Trade }

    public sealed class ResourceDef : ContentDef
    {
        public string Kind { get; set; }
        public int Nutrition { get; set; }
        public int Value { get; set; }
        public string Source { get; set; }

        [JsonIgnore] public ResourceKind KindCode { get; private set; }
        [JsonIgnore] public bool IsFood => Nutrition > 0 && (KindCode == ResourceKind.Food || KindCode == ResourceKind.Medicine);
        // Diet match (Bölüm 3.7): herbivores eat plant food, carnivores meat/fish, omnivores all.
        [JsonIgnore] public bool IsMeat { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (!Enum.TryParse(Kind, true, out ResourceKind k)) errors.Add($"{Id}: unknown resource kind '{Kind}'");
            KindCode = k;
            IsMeat = Id == "res.meat" || Id == "res.fish" || Id == "res.eggs";
        }
    }

    public enum BuildingCategory : byte { Center, House, Economy, Social, Military, Naval, Culture, Monument, Ruin }

    public enum BuildingReq : byte
    {
        Pop, Building, Coast, NearHills, NearForest, Livestock, NearFlowers, Language, Religion, Herbs, Capital, WarVictory,
        BeastCiv, HiveMind,
    }

    public struct BuildingRequirement
    {
        public BuildingReq Kind;
        public int Value; // Pop: count, Building: BuildingDef index
    }

    public sealed class BuildingDef : ContentDef
    {
        public string Category { get; set; }
        public string Size { get; set; }            // cells "WxH", 1 cell = 3x3 tiles (DECISIONS #13); "*" = any (ruins)
        public int Hp { get; set; }
        public Dictionary<string, int> Cost { get; set; }
        public string Requires { get; set; }
        public string Function { get; set; }
        public int MaxPerCity { get; set; }
        public int JobSlots { get; set; }
        public int Capacity { get; set; }           // residents (house category)

        public const int CellTiles = 3;

        [JsonIgnore] public BuildingCategory CategoryCode { get; private set; }
        [JsonIgnore] public int W { get; private set; }   // tiles
        [JsonIgnore] public int H { get; private set; }
        [JsonIgnore] public int[] CostRes { get; private set; } = Array.Empty<int>();
        [JsonIgnore] public int[] CostAmount { get; private set; } = Array.Empty<int>();
        [JsonIgnore] public BuildingRequirement[] Requirements { get; private set; } = Array.Empty<BuildingRequirement>();
        [JsonIgnore] public int TotalCost { get; private set; }

        public bool IsHouse => CategoryCode == BuildingCategory.House;
        public bool IsCenter => CategoryCode == BuildingCategory.Center;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (!Enum.TryParse(Category, true, out BuildingCategory c)) errors.Add($"{Id}: unknown building category '{Category}'");
            CategoryCode = c;

            if (Size == "*") { W = CellTiles; H = CellTiles; }
            else
            {
                var parts = (Size ?? "").Split('x');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int w) || !int.TryParse(parts[1], out int h) || w <= 0 || h <= 0)
                    errors.Add($"{Id}: bad size '{Size}'");
                else { W = w * CellTiles; H = h * CellTiles; }
            }

            var res = new List<int>();
            var amount = new List<int>();
            if (Cost != null)
                foreach (var kv in Cost)
                {
                    int r = db.Resources.IdOrDefault(kv.Key);
                    if (r < 0) { errors.Add($"{Id}: unknown cost resource '{kv.Key}'"); continue; }
                    res.Add(r);
                    amount.Add(kv.Value);
                    TotalCost += kv.Value;
                }
            CostRes = res.ToArray();
            CostAmount = amount.ToArray();
        }

        // Second pass: requirements name other buildings.
        internal void ResolveRequirements(ContentDB db, List<string> errors)
        {
            var list = new List<BuildingRequirement>();
            foreach (var raw in (Requires ?? "").Split('|'))
            {
                string r = raw.Trim();
                if (r.Length == 0) continue;
                if (r.StartsWith("pop:", StringComparison.Ordinal))
                {
                    if (int.TryParse(r.Substring(4), out int pop)) list.Add(new BuildingRequirement { Kind = BuildingReq.Pop, Value = pop });
                    else errors.Add($"{Id}: bad requirement '{r}'");
                    continue;
                }
                var kind = r switch
                {
                    "kıyı" => BuildingReq.Coast,
                    "hills/mountain yakın" => BuildingReq.NearHills,
                    "orman yakın" => BuildingReq.NearForest,
                    "evcil hayvan" => BuildingReq.Livestock,
                    "çiçek yakın" => BuildingReq.NearFlowers,
                    "dil" => BuildingReq.Language,
                    "din" => BuildingReq.Religion,
                    "herbs" => BuildingReq.Herbs,
                    "başkent" => BuildingReq.Capital,
                    "savaş zaferi" => BuildingReq.WarVictory,
                    "hayvan kökenli uygarlık" => BuildingReq.BeastCiv,
                    "kovan zihni" => BuildingReq.HiveMind,
                    _ => BuildingReq.Building,
                };
                if (kind != BuildingReq.Building)
                {
                    list.Add(new BuildingRequirement { Kind = kind });
                    continue;
                }
                string id = r == "farm" ? "bld.farm_shed" : "bld." + r;
                int b = db.Buildings.IdOrDefault(id);
                if (b < 0) errors.Add($"{Id}: unknown requirement '{r}'");
                else list.Add(new BuildingRequirement { Kind = BuildingReq.Building, Value = b });
            }
            Requirements = list.ToArray();
        }
    }

    public sealed class BuildingStyleDef : ContentDef
    {
        public string Description { get; set; }
        public string Accent { get; set; }
        [JsonIgnore] public Color32 AccentColor { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (!HexColor.TryParse(Accent, out var c)) errors.Add($"{Id}: bad accent '{Accent}'");
            AccentColor = c;
        }
    }

    public sealed class JobDef : ContentDef
    {
        public string Task { get; set; }
        public string QuotaRule { get; set; }
        public string RequiresBuilding { get; set; }
        [JsonIgnore] public int RequiredBuilding { get; private set; } = -1;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (string.IsNullOrEmpty(RequiresBuilding)) return;
            RequiredBuilding = db.Buildings.IdOrDefault(RequiresBuilding);
            if (RequiredBuilding < 0) errors.Add($"{Id}: unknown building '{RequiresBuilding}'");
        }
    }

    public enum EquipSlot : byte { Weapon, Armor, Helmet, Shield, Boots, Ring, Amulet }

    public sealed class EquipmentTypeDef : ContentDef
    {
        public string Slot { get; set; }
        public EffectBlock BaseEffects { get; set; }
        public bool UsesMaterial { get; set; }

        [JsonIgnore] public EquipSlot SlotCode { get; private set; }
        [JsonIgnore] public float[] AddStats { get; private set; }  // by StatId
        [JsonIgnore] public float[] PctStats { get; private set; }  // percent, by StatId

        // Keys that are not stats (block, knockback, waterDmg) are behaviour keys for later chapters (Bölüm 3.3).
        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (!Enum.TryParse(Slot, true, out EquipSlot s)) errors.Add($"{Id}: unknown slot '{Slot}'");
            SlotCode = s;
            AddStats = new float[(int)StatId.Count];
            PctStats = new float[(int)StatId.Count];
            if (BaseEffects?.Add != null)
                foreach (var kv in BaseEffects.Add)
                    if (StatKeys.TryGet(kv.Key, out var id)) AddStats[(int)id] += kv.Value;
            if (BaseEffects?.Pct != null)
                foreach (var kv in BaseEffects.Pct)
                    if (StatKeys.TryGet(kv.Key, out var id)) PctStats[(int)id] += kv.Value;
        }
    }

    public sealed class EffectBlock
    {
        public Dictionary<string, float> Add { get; set; }
        public Dictionary<string, float> Pct { get; set; }
    }

    public sealed class MaterialDef : ContentDef
    {
        public float Multiplier { get; set; }
        public int Tier { get; set; }
        public string Special { get; set; }
        // Resource the smith consumes for this material (mat.iron -> res.iron); -1 = free (wood, bone: DECISIONS #56)
        [JsonIgnore] public int Resource { get; private set; } = -1;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            string res = "res." + (Id.StartsWith("mat.", StringComparison.Ordinal) ? Id.Substring(4) : Id);
            Resource = db.Resources.IdOrDefault(res);
        }
    }

    public sealed class ItemQualityDef : ContentDef
    {
        public float Multiplier { get; set; }
        public float BaseChance { get; set; }
    }

    public sealed class HappinessEventDef : ContentDef
    {
        public int Value { get; set; }
    }
}

namespace PG.Content
{
    // name_sets.json (single object): phonology sets per language trait + ruler titles (Bölüm 6.2).
    public sealed class NameSets
    {
        public sealed class Set
        {
            public string[] Onset = Array.Empty<string>(), Vowel = Array.Empty<string>(), Coda = Array.Empty<string>(), Pattern = Array.Empty<string>();
        }

        public readonly Dictionary<string, Set> Sets = new Dictionary<string, Set>();
        public readonly List<string> SetIds = new List<string>();
        public readonly List<(int MinCities, string Title)> RulerTitles = new List<(int, string)>();

        public static NameSets From(Newtonsoft.Json.Linq.JObject o)
        {
            var r = new NameSets();
            if (o == null) return r;
            if (o["sets"] is Newtonsoft.Json.Linq.JObject sets)
                foreach (var kv in sets)
                {
                    var s = new Set
                    {
                        Onset = Split((string)kv.Value["onset"]),
                        Vowel = Split((string)kv.Value["vowel"]),
                        Coda = Split((string)kv.Value["coda"]),
                        Pattern = Split((string)kv.Value["pattern"]),
                    };
                    r.Sets[kv.Key] = s;
                    r.SetIds.Add(kv.Key);
                }
            if (o["rulerTitles"] is Newtonsoft.Json.Linq.JArray titles)
                foreach (var t in titles) r.RulerTitles.Add(((int?)t["minCities"] ?? 1, (string)t["title"] ?? ""));
            return r;
        }

        // Keeps empty entries: "n,r,l,s,t," means a coda may also be empty.
        static string[] Split(string s) => string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Split(',');
    }
}

namespace PG.Content
{
    // kingdom_traits.json: effects are behaviour keys read by the meta systems (colonizeChance, armySize, warChance, ...).
    public sealed class KingdomTraitDef : ContentDef
    {
        public EffectBlock Effects { get; set; }

        public float Pct(string key) => Effects?.Pct != null && Effects.Pct.TryGetValue(key, out float v) ? v : 0f;
        public float Add(string key) => Effects?.Add != null && Effects.Add.TryGetValue(key, out float v) ? v : 0f;
    }

    public sealed class WarTypeDef : ContentDef { }
}
