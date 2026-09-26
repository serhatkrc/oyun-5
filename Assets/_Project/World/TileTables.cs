using System;
using System.Collections.Generic;
using PG.Content;
using Unity.Collections;

namespace PG.World
{
    // Per-type lookup tables in Burst-friendly form, built once from ContentDB.
    // Tile tables are indexed by tile id; feature tables by Feature map value (0 = none); biome tables by Biome map value (0 = none).
    public sealed class TileTables : IDisposable
    {
        public const byte NoStep = 255;
        public const byte MaterialLand = 0, MaterialWater = 1, MaterialLava = 2;
        public const int FallbackReliefLevel = 4; // relief level for tiles without a level (lava, field, ...)

        // --- tiles ---
        public NativeArray<ushort> TypeFlags;
        public NativeArray<sbyte> Level;
        public NativeArray<byte> ReliefLevel;
        public NativeArray<byte> CanHaveBiome;
        public NativeArray<byte> RaiseTo;
        public NativeArray<byte> LowerTo;
        public NativeArray<byte> Material;
        public NativeArray<float> ReliefShade;
        public NativeArray<float> MoveCost; // walk cost multiplier (tiles.json moveCost), 0 = not walkable
        public NativeArray<byte> FreezeTo, MeltTo, BurnTo, RecoverTo, DecayTo, DecayToHigh, QuenchTo; // NoStep = none
        public NativeArray<float> RecoverChance, DecayFeatureChance;
        public NativeArray<byte> RecoverNeedsWater, Flows, Ignites, Spreads;
        public NativeArray<ushort> DecayTicks, DecayFeature;
        public NativeArray<sbyte> ZoneHeat;
        public NativeArray<byte> Active; // 1 = tracked by the nature systems while present (lava, goo)

        // --- features ---
        public NativeArray<byte> FeatureKind;       // FeatureDef.Kind*
        public NativeArray<byte> FeatureBurnable;
        public NativeArray<byte> FeatureAquatic;
        public NativeArray<ushort> FeatureBurnsInto;
        public NativeArray<byte> FeatureResource;   // initial resource (0-63)

        // --- biomes ---
        public NativeArray<byte> BiomeWaterOnly, BiomeFireproof, BiomeSnowCover, BiomeEffectCode;
        public NativeArray<sbyte> BiomeTempOffset;

        readonly List<IDisposable> _all = new List<IDisposable>(48);

        public TileTables(ContentDB db)
        {
            int n = db.Tiles.Count;
            TypeFlags = Make<ushort>(n);
            Level = Make<sbyte>(n);
            ReliefLevel = Make<byte>(n);
            CanHaveBiome = Make<byte>(n);
            RaiseTo = Make<byte>(n);
            LowerTo = Make<byte>(n);
            Material = Make<byte>(n);
            ReliefShade = Make<float>(n);
            MoveCost = Make<float>(n);
            FreezeTo = Make<byte>(n); MeltTo = Make<byte>(n); BurnTo = Make<byte>(n); RecoverTo = Make<byte>(n);
            DecayTo = Make<byte>(n); DecayToHigh = Make<byte>(n); QuenchTo = Make<byte>(n);
            RecoverChance = Make<float>(n); DecayFeatureChance = Make<float>(n);
            RecoverNeedsWater = Make<byte>(n); Flows = Make<byte>(n); Ignites = Make<byte>(n); Spreads = Make<byte>(n);
            DecayTicks = Make<ushort>(n); DecayFeature = Make<ushort>(n);
            ZoneHeat = Make<sbyte>(n);
            Active = Make<byte>(n);

            for (int i = 0; i < n; i++)
            {
                var t = db.Tiles[i];
                TypeFlags[i] = (ushort)t.DerivedFlags;
                Level[i] = (sbyte)t.Level;
                ReliefLevel[i] = (byte)(t.IsLeveled ? t.Level : FallbackReliefLevel);
                CanHaveBiome[i] = B(t.CanHaveBiome);
                RaiseTo[i] = Step(t.RaiseToId);
                LowerTo[i] = Step(t.LowerToId);
                Material[i] = t.RenderMaterial == "water" ? MaterialWater : t.RenderMaterial == "lava" ? MaterialLava : MaterialLand;
                ReliefShade[i] = t.ReliefShade;
                MoveCost[i] = t.Walkable ? Math.Max(0.1f, t.MoveCost) : 0f;
                FreezeTo[i] = Step(t.FreezeToId);
                MeltTo[i] = Step(t.MeltToId);
                BurnTo[i] = Step(t.BurnToId);
                RecoverTo[i] = Step(t.RecoverToId);
                DecayTo[i] = Step(t.DecayToId);
                DecayToHigh[i] = Step(t.DecayToHighId);
                QuenchTo[i] = Step(t.QuenchToId);
                RecoverChance[i] = t.RecoverChance;
                DecayFeatureChance[i] = t.DecayFeatureChance;
                RecoverNeedsWater[i] = B(t.RecoverNeedsWater);
                Flows[i] = B(t.Flows);
                Ignites[i] = B(t.Ignites);
                Spreads[i] = B(t.Spreads);
                DecayTicks[i] = (ushort)Math.Min(t.DecayTicks, ushort.MaxValue);
                DecayFeature[i] = t.DecayFeatureValue;
                ZoneHeat[i] = (sbyte)Math.Clamp(t.ZoneHeat, sbyte.MinValue, sbyte.MaxValue);
                Active[i] = B(t.DecayTicks > 0 || t.Flows || t.Spreads);
            }

            int nf = db.Features.Count + 1;
            FeatureKind = Make<byte>(nf);
            FeatureBurnable = Make<byte>(nf);
            FeatureAquatic = Make<byte>(nf);
            FeatureBurnsInto = Make<ushort>(nf);
            FeatureResource = Make<byte>(nf);
            for (int i = 0; i < db.Features.Count; i++)
            {
                var f = db.Features[i];
                int v = f.MapValue;
                FeatureKind[v] = f.KindCode;
                FeatureBurnable[v] = B(f.Burnable);
                FeatureAquatic[v] = B(f.Aquatic);
                FeatureBurnsInto[v] = f.BurnsIntoValue;
                FeatureResource[v] = (byte)Math.Clamp(f.TotalYield * 4, 1, 63);
            }

            int nb = db.Biomes.Count + 1;
            BiomeWaterOnly = Make<byte>(nb);
            BiomeFireproof = Make<byte>(nb);
            BiomeSnowCover = Make<byte>(nb);
            BiomeEffectCode = Make<byte>(nb);
            BiomeTempOffset = Make<sbyte>(nb);
            for (int i = 0; i < db.Biomes.Count; i++)
            {
                var b = db.Biomes[i];
                int v = b.MapValue;
                BiomeWaterOnly[v] = B(b.WaterOnly);
                BiomeFireproof[v] = B(b.Fireproof);
                BiomeSnowCover[v] = B(b.SnowCover);
                BiomeEffectCode[v] = (byte)b.EffectKind;
                BiomeTempOffset[v] = (sbyte)Math.Clamp(b.TempOffset, sbyte.MinValue, sbyte.MaxValue);
            }
        }

        // Can this biome live on this ground? Water-only biomes (coral) need walkable water, others need land.
        public bool BiomeFits(byte ground, byte biome)
        {
            if (biome == 0) return true;
            if (CanHaveBiome[ground] == 0) return false;
            bool water = (TypeFlags[ground] & (ushort)TileFlags.Water) != 0;
            return water == (BiomeWaterOnly[biome] != 0);
        }

        // Aquatic features live on water, all others on dry ground.
        public bool FeatureFits(byte ground, ushort feature)
        {
            if (feature == 0) return true;
            bool water = (TypeFlags[ground] & (ushort)TileFlags.Water) != 0;
            return water == (FeatureAquatic[feature] != 0);
        }

        NativeArray<T> Make<T>(int n) where T : struct
        {
            var a = new NativeArray<T>(n, Allocator.Persistent);
            _all.Add(a);
            return a;
        }

        static byte B(bool v) => v ? (byte)1 : (byte)0;
        static byte Step(int id) => id < 0 ? NoStep : (byte)id;

        public void Dispose()
        {
            foreach (var a in _all) a.Dispose();
            _all.Clear();
        }
    }
}
