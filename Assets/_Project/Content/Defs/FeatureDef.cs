using System.Collections.Generic;
using Newtonsoft.Json;

namespace PG.Content
{
    public sealed class FeatureDef : ContentDef
    {
        public string Kind { get; set; }                  // tree, plant, crop, ore
        public Dictionary<string, int> Yields { get; set; }
        public bool Burnable { get; set; }
        public float VeinWeight { get; set; }             // worldgen ore vein weight (0 = never)
        public bool Aquatic { get; set; }                 // lives on water tiles (coral, reed)
        public string BurnsInto { get; set; }             // what fire leaves behind ("" = nothing)
        public string Note { get; set; }

        public const byte KindNone = 0, KindTree = 1, KindPlant = 2, KindCrop = 3, KindOre = 4;

        // Value stored in WorldMap.Feature (0 = no feature).
        [JsonIgnore] public ushort MapValue => (ushort)(Index + 1);
        [JsonIgnore] public ushort BurnsIntoValue { get; private set; }
        [JsonIgnore] public byte KindCode { get; private set; }
        [JsonIgnore] public int TotalYield { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            KindCode = Kind == "tree" ? KindTree : Kind == "plant" ? KindPlant : Kind == "crop" ? KindCrop : Kind == "ore" ? KindOre : KindNone;
            if (KindCode == KindNone) errors.Add($"{Id}: unknown kind '{Kind}'");
            int total = 0;
            if (Yields != null)
                foreach (var y in Yields.Values) total += y;
            TotalYield = total;
            if (!string.IsNullOrEmpty(BurnsInto))
            {
                if (db.Features.TryGet(BurnsInto, out var f)) BurnsIntoValue = f.MapValue;
                else errors.Add($"{Id}.burnsInto: unknown feature '{BurnsInto}'");
            }
        }
    }
}
