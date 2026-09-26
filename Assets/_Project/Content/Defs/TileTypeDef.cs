using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace PG.Content
{
    public sealed class TileTypeDef : ContentDef
    {
        public const int NoLevel = -1;

        public int Level { get; set; } = NoLevel;
        public bool Walkable { get; set; }
        public bool Water { get; set; }
        public bool Buildable { get; set; }
        public bool Burnable { get; set; }
        public bool CanHaveBiome { get; set; }
        public float MoveCost { get; set; }
        public string[] Colors { get; set; }
        public float ReliefShade { get; set; } = 0.06f;
        public string OnRaiseBecomes { get; set; }
        public string OnLowerBecomes { get; set; }
        public string RenderMaterial { get; set; } // "", "water" or "lava" (shader material code)
        public string Note { get; set; }

        // Nature rules (Bölüm 2.5, 2.8-2.10)
        public string FreezeTo { get; set; }
        public string MeltTo { get; set; }
        public string OnBurnBecomes { get; set; }
        public string RecoverTo { get; set; }
        public float RecoverChance { get; set; }
        public bool RecoverNeedsWater { get; set; }
        public string DecayTo { get; set; }
        public string DecayToIfHigh { get; set; }
        public int DecayTicks { get; set; }
        public string DecayFeature { get; set; }
        public float DecayFeatureChance { get; set; }
        public bool Flows { get; set; }
        public string QuenchTo { get; set; }
        public bool Ignites { get; set; }
        public int ZoneHeat { get; set; }
        public bool Spreads { get; set; }

        [JsonIgnore] public byte NumericId => (byte)Index;
        [JsonIgnore] public TileFlags DerivedFlags { get; private set; }
        [JsonIgnore] public Color32[] ParsedColors { get; private set; }
        [JsonIgnore] public int RaiseToId { get; private set; } = -1;
        [JsonIgnore] public int LowerToId { get; private set; } = -1;
        [JsonIgnore] public bool IsLeveled => Level >= 0;
        [JsonIgnore] public int FreezeToId { get; private set; } = -1;
        [JsonIgnore] public int MeltToId { get; private set; } = -1;
        [JsonIgnore] public int BurnToId { get; private set; } = -1;
        [JsonIgnore] public int RecoverToId { get; private set; } = -1;
        [JsonIgnore] public int DecayToId { get; private set; } = -1;
        [JsonIgnore] public int DecayToHighId { get; private set; } = -1;
        [JsonIgnore] public int QuenchToId { get; private set; } = -1;
        [JsonIgnore] public ushort DecayFeatureValue { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var f = TileFlags.None;
            if (Walkable) f |= TileFlags.Walkable;
            if (Water) f |= TileFlags.Water;
            if (Water && !Walkable) f |= TileFlags.DeepWater;
            if (Burnable) f |= TileFlags.Burnable;
            if (Buildable) f |= TileFlags.Buildable;
            DerivedFlags = f;

            ParsedColors = HexColor.ParseVariants(Colors, Id, errors);
            RaiseToId = ResolveTile(db, OnRaiseBecomes, "onRaiseBecomes", errors);
            LowerToId = ResolveTile(db, OnLowerBecomes, "onLowerBecomes", errors);
            FreezeToId = ResolveTile(db, FreezeTo, "freezeTo", errors);
            MeltToId = ResolveTile(db, MeltTo, "meltTo", errors);
            BurnToId = ResolveTile(db, OnBurnBecomes, "onBurnBecomes", errors);
            RecoverToId = ResolveTile(db, RecoverTo, "recoverTo", errors);
            DecayToId = ResolveTile(db, DecayTo, "decayTo", errors);
            DecayToHighId = ResolveTile(db, DecayToIfHigh, "decayToIfHigh", errors);
            QuenchToId = ResolveTile(db, QuenchTo, "quenchTo", errors);
            if (DecayTicks > 0 && DecayToId < 0) errors.Add($"{Id}: decayTicks without decayTo");
            if (!string.IsNullOrEmpty(DecayFeature))
            {
                if (db.Features.TryGet(DecayFeature, out var feature)) DecayFeatureValue = feature.MapValue;
                else errors.Add($"{Id}.decayFeature: unknown feature '{DecayFeature}'");
            }
        }

        int ResolveTile(ContentDB db, string key, string field, List<string> errors)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            if (db.Tiles.TryGet(key, out var t)) return t.Index;
            errors.Add($"{Id}.{field}: unknown tile '{key}'");
            return -1;
        }
    }
}
