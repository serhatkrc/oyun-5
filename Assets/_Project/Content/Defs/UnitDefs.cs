using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PG.Content
{
    // Bölüm 3.2: effect keys the stat formula knows. Everything else is a behaviour key read by the owning system.
    public enum StatId : byte
    {
        Hp, Dmg, Armor, Speed, AtkSpd, Crit, Range, Dodge, Lifespan, Fertility, Intel, Diplo, Warfare, Steward, Luck,
        Mana, ManaRegen, Regen, Stamina, HungerRate, SleepNeed, Sight, Size, KnockbackResist, DiseaseResist, SpellPower,
        Xp, Happiness, FleeThreshold, MateChance, Lifesteal, Block, Stealth, DmgTaken, SwimSpeed,
        Count,
    }

    [Flags]
    public enum UnitFlags : uint
    {
        None = 0,
        Fly = 1 << 0,
        Swim = 1 << 1,
        Undead = 1 << 2,
        NoAging = 1 << 3,
        NoNeeds = 1 << 4,
        AttackAll = 1 << 5,
        BreatheWater = 1 << 6,
        Climb = 1 << 7,
        Phase = 1 << 8,
        Edited = 1 << 9,
        // status-driven (cleared and re-applied with the stats)
        Stunned = 1 << 16,
        Asleep = 1 << 17,
        Immobile = 1 << 18,
        Afraid = 1 << 19,
        Untargetable = 1 << 20,
        PlayerControlled = 1 << 21,
        Charmed = 1 << 22,
        StatusMask = Stunned | Asleep | Immobile | Afraid | Untargetable | PlayerControlled | Charmed,
    }

    public enum Diet : byte { None, Herb, Carn, Omni, Brains, Blood }
    public enum Habitat : byte { Land, Air, Amph, Water, Under }
    public enum Reproduction : byte { None, Live, Egg, Split, Spore, Tumor, Infect, Assimilate, Summon, Seed, Meta }

    public struct StatMod
    {
        public StatId Stat;
        public float Add;
        public float Pct;
    }

    public static class StatKeys
    {
        static readonly Dictionary<string, StatId> Map = BuildMap();

        static Dictionary<string, StatId> BuildMap()
        {
            var map = new Dictionary<string, StatId>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < (int)StatId.Count; i++) map[((StatId)i).ToString()] = (StatId)i;
            map["damage"] = StatId.Dmg;
            map["attackSpeed"] = StatId.AtkSpd;
            return map;
        }

        public static bool TryGet(string key, out StatId id) => Map.TryGetValue(key, out id);

        public static readonly StatId[] AllStats = { StatId.Hp, StatId.Dmg, StatId.Armor, StatId.Speed, StatId.AtkSpd, StatId.Dodge, StatId.Crit };

        // Parses {"add": {...}, "pct": {...}} into stat mods; unknown keys go to `behavior` (key -> summed add/pct).
        public static StatMod[] Parse(JObject add, JObject pct, Dictionary<string, float> behavior)
        {
            var mods = new List<StatMod>();
            Collect(add, false, mods, behavior);
            Collect(pct, true, mods, behavior);
            return mods.ToArray();
        }

        static void Collect(JObject obj, bool isPct, List<StatMod> mods, Dictionary<string, float> behavior)
        {
            if (obj == null) return;
            foreach (var p in obj.Properties())
            {
                float v = p.Value.Type == JTokenType.Float || p.Value.Type == JTokenType.Integer ? p.Value.Value<float>() : 0f;
                if (string.Equals(p.Name, "allStats", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var s in AllStats) mods.Add(isPct ? new StatMod { Stat = s, Pct = v } : new StatMod { Stat = s, Add = v });
                    continue;
                }
                if (TryGet(p.Name, out var id)) mods.Add(isPct ? new StatMod { Stat = id, Pct = v } : new StatMod { Stat = id, Add = v });
                else if (behavior != null)
                {
                    string key = (isPct ? "pct." : "add.") + p.Name;
                    behavior.TryGetValue(key, out float old);
                    behavior[key] = old + v;
                }
            }
        }

        public static UnitFlags Flag(string name)
        {
            switch (name)
            {
                case "fly": case "flight": return UnitFlags.Fly;
                case "swim": return UnitFlags.Swim;
                case "undead": return UnitFlags.Undead;
                case "no_aging": return UnitFlags.NoAging;
                case "no_needs": return UnitFlags.NoNeeds;
                case "attack_all": return UnitFlags.AttackAll;
                case "breathe_water": return UnitFlags.BreatheWater;
                case "climb": return UnitFlags.Climb;
                case "phase": return UnitFlags.Phase;
                case "edited": return UnitFlags.Edited;
                case "stunned": return UnitFlags.Stunned;
                case "asleep": return UnitFlags.Asleep;
                case "immobile": return UnitFlags.Immobile;
                case "flee": return UnitFlags.Afraid;
                case "untargetable": return UnitFlags.Untargetable;
                case "player_controlled": return UnitFlags.PlayerControlled;
                case "ally_of_caster": return UnitFlags.Charmed;
                default: return UnitFlags.None;
            }
        }
    }

    public sealed class SpeciesAbilities
    {
        public string[] Grants { get; set; }
        public string[] Spells { get; set; }
        public string[] Flags { get; set; }
        public string[] Immunes { get; set; }
    }

    // species.json (Bölüm 3)
    public sealed class SpeciesDef : ContentDef
    {
        public string Category { get; set; }
        public bool SapientAtStart { get; set; }
        public bool CanEvolve { get; set; }
        [JsonProperty("diet")] public string DietName { get; set; }
        [JsonProperty("habitat")] public string HabitatName { get; set; }
        [JsonProperty("stats")] public Dictionary<string, float> StatValues { get; set; }
        [JsonProperty("reproduction")] public string ReproductionName { get; set; }
        public SpeciesAbilities Abilities { get; set; }
        public string Note { get; set; }

        [JsonIgnore] public Diet Diet { get; private set; }
        [JsonIgnore] public Habitat Habitat { get; private set; }
        [JsonIgnore] public Reproduction Reproduction { get; private set; }
        [JsonIgnore] public float[] BaseStats { get; private set; }
        [JsonIgnore] public int[] GrantTraits { get; private set; }  // UnitTraitDef indices
        [JsonIgnore] public int[] SpellIds { get; private set; }     // SpellDef indices
        [JsonIgnore] public UnitFlags Flags { get; private set; }
        [JsonIgnore] public string[] Immunes { get; private set; }
        [JsonIgnore] public bool IsMonster => Category == "monster";
        [JsonIgnore] public bool IsUndeadCategory => Category == "undead";
        [JsonIgnore] public bool IsCiv => Category == "civ" || SapientAtStart;

        public const float PredatorSight = 14f;

        // Stat defaults when species.json does not set a value.
        public static float DefaultStat(StatId id)
        {
            switch (id)
            {
                case StatId.AtkSpd: return 1f;
                case StatId.Crit: return 5f;
                case StatId.Range: return 1f;
                case StatId.Fertility: return 1f;
                case StatId.Stamina: return 100f;
                case StatId.HungerRate: return 1f;
                case StatId.SleepNeed: return 1f;
                case StatId.Sight: return 8f;
                case StatId.Size: return 1f;
                case StatId.FleeThreshold: return 1f;
                case StatId.MateChance: return 1f;
                case StatId.SwimSpeed: return 1f;
                case StatId.Mana: return 0f;
                case StatId.ManaRegen: return 0.05f;
                case StatId.DmgTaken: return 1f; // damage multiplier; st.shielded -60% -> 0.4
                default: return 0f;
            }
        }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            Diet = Parse(DietName, Diet.None, errors, "diet");
            Habitat = Parse(HabitatName, Habitat.Land, errors, "habitat");
            Reproduction = Parse(ReproductionName, Reproduction.None, errors, "reproduction");

            BaseStats = new float[(int)StatId.Count];
            for (int i = 0; i < BaseStats.Length; i++) BaseStats[i] = DefaultStat((StatId)i);
            if (StatValues != null)
                foreach (var kv in StatValues)
                {
                    if (StatKeys.TryGet(kv.Key, out var id)) BaseStats[(int)id] = kv.Value;
                    else errors.Add($"{Id}: unknown stat '{kv.Key}'");
                }

            // hunters see further than grazers unless species.json says otherwise (DECISIONS #52)
            if (Diet == Diet.Carn && (StatValues == null || !StatValues.ContainsKey("sight"))) BaseStats[(int)StatId.Sight] = PredatorSight;

            var a = Abilities ?? new SpeciesAbilities();
            var grants = new List<int>();
            foreach (var g in a.Grants ?? Array.Empty<string>())
            {
                int t = db.UnitTraits.IdOrDefault("tr." + g);
                if (t >= 0) grants.Add(t);
                else errors.Add($"{Id}: unknown granted trait 'tr.{g}'");
            }
            GrantTraits = grants.ToArray();

            var spells = new List<int>();
            foreach (var s in a.Spells ?? Array.Empty<string>())
            {
                int sp = db.Spells.IdOrDefault("spell." + s);
                if (sp >= 0) spells.Add(sp);
                else errors.Add($"{Id}: unknown spell 'spell.{s}'");
            }
            SpellIds = spells.ToArray();

            var flags = UnitFlags.None;
            foreach (var f in a.Flags ?? Array.Empty<string>()) flags |= StatKeys.Flag(f);
            if (Habitat == Habitat.Air) flags |= UnitFlags.Fly;
            if (Habitat == Habitat.Amph || Habitat == Habitat.Water) flags |= UnitFlags.Swim;
            if (Habitat == Habitat.Water) flags |= UnitFlags.BreatheWater;
            if (Category == "undead") flags |= UnitFlags.Undead;
            if (BaseStats[(int)StatId.Lifespan] <= 0f) flags |= UnitFlags.NoAging;
            if (Diet == Diet.None) flags |= UnitFlags.NoNeeds;
            Flags = flags;
            Immunes = a.Immunes ?? Array.Empty<string>();
        }

        T Parse<T>(string name, T fallback, List<string> errors, string field) where T : struct
        {
            if (string.IsNullOrEmpty(name)) return fallback;
            if (Enum.TryParse(name, true, out T value)) return value;
            errors.Add($"{Id}: unknown {field} '{name}'");
            return fallback;
        }
    }

    // unit_traits.json
    public sealed class UnitTraitDef : ContentDef
    {
        public string Group { get; set; }
        public string Rarity { get; set; }
        public JObject Effects { get; set; }
        public string Opposite { get; set; }
        public float InheritChance { get; set; }

        [JsonIgnore] public StatMod[] Mods { get; private set; }
        [JsonIgnore] public Dictionary<string, float> Behavior { get; private set; }
        [JsonIgnore] public UnitFlags Flags { get; private set; }
        [JsonIgnore] public int[] SpellIds { get; private set; }
        [JsonIgnore] public string[] Neurons { get; private set; }
        [JsonIgnore] public string[] Immunes { get; private set; }
        [JsonIgnore] public int OppositeId { get; private set; } = -1;
        [JsonIgnore] public int OnHitStatus { get; private set; } = -1; // StatusEffectDef index
        [JsonIgnore] public float OnHitChance { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var e = Effects ?? new JObject();
            Behavior = new Dictionary<string, float>();
            Mods = StatKeys.Parse(e["add"] as JObject, e["pct"] as JObject, Behavior);

            var flags = UnitFlags.None;
            foreach (var f in Strings(e, "flags")) flags |= StatKeys.Flag(f);
            Flags = flags;
            Neurons = Strings(e, "neurons");
            Immunes = Strings(e, "immunes");

            var spells = new List<int>();
            foreach (var s in Strings(e, "spells"))
            {
                int sp = db.Spells.IdOrDefault("spell." + s);
                if (sp >= 0) spells.Add(sp);
                else errors.Add($"{Id}: unknown spell 'spell.{s}'");
            }
            SpellIds = spells.ToArray();

            if (!string.IsNullOrEmpty(Opposite)) OppositeId = db.UnitTraits.IdOrDefault(Opposite);

            // onHit "freeze:10" -> st.frozen 10%, "poison:30" -> st.poisoned 30%
            string onHit = e.Value<string>("onHit");
            if (!string.IsNullOrEmpty(onHit))
            {
                var parts = onHit.Split(':');
                string status = parts[0] == "freeze" ? "st.frozen" : parts[0] == "poison" ? "st.poisoned" : "st." + parts[0];
                OnHitStatus = db.StatusEffects.IdOrDefault(status);
                OnHitChance = parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float c) ? c / 100f : 1f;
                if (OnHitStatus < 0) errors.Add($"{Id}: onHit status '{status}' not found");
            }
        }

        public bool Immune(string what) => Array.IndexOf(Immunes, what) >= 0;

        static string[] Strings(JObject e, string key)
        {
            if (!(e[key] is JArray arr)) return Array.Empty<string>();
            var result = new string[arr.Count];
            for (int i = 0; i < arr.Count; i++) result[i] = arr[i].Value<string>();
            return result;
        }
    }

    // status_effects.json: "hp:-3/tick|luck:+20|speed:-50%|flag:stunned"
    public sealed class StatusEffectDef : ContentDef
    {
        public int DurationMonths { get; set; }
        public string Effect { get; set; }
        public string Note { get; set; }

        [JsonIgnore] public StatMod[] Mods { get; private set; }
        [JsonIgnore] public UnitFlags Flags { get; private set; }
        [JsonIgnore] public float HpPerTick { get; private set; }   // negative = damage
        [JsonIgnore] public int HpEveryTicks { get; private set; } = 1;
        [JsonIgnore] public float HappinessPerMonth { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var mods = new List<StatMod>();
            var flags = UnitFlags.None;
            foreach (var raw in (Effect ?? "").Split('|'))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                int colon = part.IndexOf(':');
                if (colon < 0) { errors.Add($"{Id}: bad effect '{part}'"); continue; }
                string key = part.Substring(0, colon), value = part.Substring(colon + 1);
                if (key == "flag") { flags |= StatKeys.Flag(value); continue; }

                if (key == "hp" && value.Contains("/"))
                {
                    var slash = value.Split('/');
                    string every = slash[1].Replace("tick", "");
                    HpPerTick = Num(slash[0]);
                    HpEveryTicks = every.Length == 0 ? 1 : Math.Max(1, (int)Num(every));
                    continue;
                }
                bool pct = value.EndsWith("%");
                float v = Num(pct ? value.Substring(0, value.Length - 1) : value);
                if (key == "happiness") { HappinessPerMonth = v; continue; }
                if (StatKeys.TryGet(key, out var id)) mods.Add(pct ? new StatMod { Stat = id, Pct = v } : new StatMod { Stat = id, Add = v });
                else if (key == "allStats") foreach (var s in StatKeys.AllStats) mods.Add(new StatMod { Stat = s, Pct = v });
                // other keys (plotChance, fireResist, mutation...) belong to later chapters
            }
            Mods = mods.ToArray();
            Flags = flags;
        }

        static float Num(string s) =>
            float.TryParse(s.Replace("+", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }

    // spells.json: behaviour lives in the Sim SpellRegistry, keyed by id.
    public sealed class SpellDef : ContentDef
    {
        public int ManaCost { get; set; }
        public int CooldownTicks { get; set; }
        public float Range { get; set; }
        public string Effect { get; set; }
    }
}
