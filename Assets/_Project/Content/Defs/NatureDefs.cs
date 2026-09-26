using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace PG.Content
{
    // CloudEffectRegistry (Bölüm 2.7)
    public enum CloudEffect : byte { Rain, Snow, Acid, Lava, Life, Storm, Ash, Blessing, Plague, Rot, Candy }

    // clouds.json (Bölüm 2.7)
    public sealed class CloudDef : ContentDef
    {
        public string Effect { get; set; }
        public string EffectCode { get; set; }
        public string Color { get; set; }
        public float NaturalWeight { get; set; }
        public bool ColdOnly { get; set; }
        public string DropTile { get; set; }
        public string DropFeature { get; set; }
        public float DropChance { get; set; } = 1f;

        [JsonIgnore] public Color32 ParsedColor { get; private set; }
        [JsonIgnore] public int DropTileId { get; private set; } = -1;
        [JsonIgnore] public ushort DropFeatureValue { get; private set; }
        [JsonIgnore] public CloudEffect EffectKind { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (System.Enum.TryParse(EffectCode, true, out CloudEffect e)) EffectKind = e;
            else errors.Add($"{Id}: unknown effectCode '{EffectCode}'");
            if (!HexColor.TryParse(Color, out var c)) errors.Add($"{Id}: invalid color '{Color}'");
            ParsedColor = c;
            if (!string.IsNullOrEmpty(DropTile))
            {
                if (db.Tiles.TryGet(DropTile, out var t)) DropTileId = t.Index;
                else errors.Add($"{Id}.dropTile: unknown tile '{DropTile}'");
            }
            if (!string.IsNullOrEmpty(DropFeature))
            {
                if (db.Features.TryGet(DropFeature, out var f)) DropFeatureValue = f.MapValue;
                else errors.Add($"{Id}.dropFeature: unknown feature '{DropFeature}'");
            }
        }
    }

    // eras.json (Bölüm 2.11)
    public sealed class EraDef : ContentDef
    {
        public int MinYears { get; set; }
        public int MaxYears { get; set; }
        public int Rate { get; set; }
        public int LoyaltyBonus { get; set; }
        public int OpinionBonus { get; set; }
        public int FertilityPct { get; set; } = 100;
        public int TempShift { get; set; }
        public int CloudIntervalMonths { get; set; } = 12;
        public int BiomeGrowthBonus { get; set; }
        public string Effects { get; set; }
        public string Tint { get; set; }
        public int DefaultSlot { get; set; } = -1;
        public float FireSpreadMul { get; set; } = 1f;
        public float PlantGrowthMul { get; set; } = 1f;
        public bool StopsBiomeGrowth { get; set; }
        public bool GlobalRain { get; set; }
        public float AshCloudChance { get; set; }
        public string[] EffectCodes { get; set; }

        [JsonIgnore] public Color32 ParsedTint { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (!HexColor.TryParse(Tint, out var c)) errors.Add($"{Id}: invalid tint '{Tint}'");
            ParsedTint = c;
            if (MinYears > MaxYears) errors.Add($"{Id}: minYears > maxYears");
            if (EffectCodes == null) EffectCodes = System.Array.Empty<string>();
        }

        public bool HasEffect(string code) => System.Array.IndexOf(EffectCodes, code) >= 0;
    }

    // disasters.json (Bölüm 2.12)
    public sealed class DisasterDef : ContentDef
    {
        public int MinWorldAge { get; set; }
        public int MinPopulation { get; set; }
        public int CooldownYears { get; set; }
        public float ChancePerYear { get; set; }
        public Dictionary<string, float> EraMultipliers { get; set; }
        public string Description { get; set; }
        public string Placement { get; set; }
        public string Power { get; set; }

        [JsonIgnore] public float[] EraMultiplierByIndex { get; private set; }
        [JsonIgnore] public int PowerIndex { get; private set; } = -1;
        [JsonIgnore] public int PlacementBiome { get; private set; } = -1;   // biome index for biomeOrLand:

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            EraMultiplierByIndex = new float[db.Eras.Count];
            for (int i = 0; i < EraMultiplierByIndex.Length; i++) EraMultiplierByIndex[i] = 1f;
            if (EraMultipliers != null)
                foreach (var kv in EraMultipliers)
                {
                    if (db.Eras.TryGet(kv.Key, out var era)) EraMultiplierByIndex[era.Index] = kv.Value;
                    else errors.Add($"{Id}.eraMultipliers: unknown era '{kv.Key}'");
                }
            if (!string.IsNullOrEmpty(Power))
            {
                if (db.Powers.TryGet(Power, out var p)) PowerIndex = p.Index;
                else errors.Add($"{Id}.power: unknown power '{Power}'");
            }
            if (string.IsNullOrEmpty(Placement)) Placement = "randomLand";
            const string biomePrefix = "biomeOrLand:";
            if (Placement.StartsWith(biomePrefix))
            {
                string biome = Placement.Substring(biomePrefix.Length);
                if (db.Biomes.TryGet(biome, out var b)) PlacementBiome = b.Index;
                else errors.Add($"{Id}.placement: unknown biome '{biome}'");
            }
        }
    }

    // world_laws.json (Bölüm 2.13 / 04). Slider = [min, max, default].
    public sealed class WorldLawDef : ContentDef
    {
        public string Group { get; set; }
        public bool Default { get; set; }
        public float[] Slider { get; set; }
        public string UnlockCondition { get; set; }

        [JsonIgnore] public bool HasSlider => Slider != null && Slider.Length == 3;
        [JsonIgnore] public float SliderDefault => HasSlider ? Slider[2] : 0f;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (Slider != null && Slider.Length != 3) errors.Add($"{Id}: slider must be [min, max, default]");
        }
    }
}
