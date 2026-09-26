using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    public static class NatureSampling
    {
        // Bölüm 2.3: clamp(tileCount / 2000, 64, 1024) random tiles per tick.
        public static int SamplesPerTick(WorldMap map) => math.clamp(map.TileCount / 2000, 64, 1024);

        public static readonly int2[] Neighbours4 = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
    }

    // Bölüm 2.4 growth rules, shared by FeatureGrowthSystem and rain drops.
    public static class FeatureGrowth
    {
        public const int MaxTreesPerZone = FeatureDensity.MaxTreesPerZone, MaxPlantsPerZone = FeatureDensity.MaxPlantsPerZone;
        // Doc values are 0.02 / 0.002 / 0.004 per sample; with ~2000 ticks between samples of one tile that is
        // centuries per stage, so they are scaled x10 (DECISIONS #29).
        public const float StageUpChance = 0.2f, SaplingChance = 0.02f, PlantChance = 0.04f;

        public static void TryGrow(NatureState nature, int x, int y, ref SimRandom rng)
        {
            var map = nature.World;
            int i = map.Index(x, y);
            float mul = nature.Mods.PlantGrowthMul;
            if (mul <= 0f) return;

            ushort f = map.Feature[i];
            if (f != 0)
            {
                byte kind = map.Tables.FeatureKind[f];
                if (kind == FeatureDef.KindOre) return;
                int stage = map.GetFeatureStage(i);
                if (stage < 3 && rng.Chance(StageUpChance * mul)) map.SetFeatureStage(x, y, stage + 1);
                return;
            }

            if (!nature.Laws.IsOn(nature.LawTreeGrowth)) return;
            byte b = map.Biome[i];
            if (b == 0 || map.Tables.BiomeEffectCode[b] == (byte)BiomeEffect.NoPlants) return;
            int z = map.ZoneIndexOf(x, y);
            if (nature.ZoneAshMonths[z] > 0) return;

            var biome = nature.Content.Biomes[b - 1];
            var zone = map.Zones[z];
            if (biome.TreeValues.Length > 0 && zone.TreeCount < MaxTreesPerZone && rng.Chance(SaplingChance * mul))
                map.SetFeature(x, y, biome.TreeValues[rng.Range(0, biome.TreeValues.Length)], 0, ChangeSource.Nature);
            else if (biome.PlantValues.Length > 0 && zone.PlantCount < MaxPlantsPerZone && rng.Chance(PlantChance * mul))
                map.SetFeature(x, y, biome.PlantValues[rng.Range(0, biome.PlantValues.Length)], 0, ChangeSource.Nature);
        }
    }

    // Bölüm 2.3
    public sealed class BiomeSpreadSystem : ISimSystem
    {
        public const float BaseChance = 0.05f;
        public const int SeedRadius = 6, SeedTries = 4;
        public const float SeedChanceMul = 3f;

        public SimPhase Phase => SimPhase.World;
        public int Order => 10;

        public void Tick(in SimContext ctx)
        {
            var nature = ctx.Nature;
            var map = ctx.World;
            ref var rng = ref ctx.Rng.Get(RngStream.Biome);

            if (ctx.Clock.IsYearStart) AgeSeeds(nature);

            float mul = nature.Mods.BiomeGrowthMul;
            if (mul <= 0f || !nature.Laws.IsOn(nature.LawBiomeSpread)) return;

            int samples = NatureSampling.SamplesPerTick(map);
            int n = map.TileCount;
            for (int s = 0; s < samples; s++)
            {
                int i = rng.Range(0, n);
                TrySpread(map, i % map.Width, i / map.Width, mul, 1f, ref rng);
            }

            for (int k = 0; k < nature.Seeds.Length; k++)
            {
                var seed = nature.Seeds[k];
                int sx = seed.Tile % map.Width, sy = seed.Tile / map.Width;
                for (int t = 0; t < SeedTries; t++)
                {
                    int x = sx + rng.Range(-SeedRadius, SeedRadius + 1), y = sy + rng.Range(-SeedRadius, SeedRadius + 1);
                    if (!map.InBounds(x, y) || map.Biome[map.Index(x, y)] != seed.Biome) continue;
                    TrySpread(map, x, y, mul, SeedChanceMul, ref rng);
                }
            }
        }

        static void TrySpread(WorldMap map, int x, int y, float mul, float chanceMul, ref SimRandom rng)
        {
            byte b = map.Biome[map.Index(x, y)];
            if (b == 0) return;
            var d = NatureSampling.Neighbours4[rng.Range(0, 4)];
            int nx = x + d.x, ny = y + d.y;
            if (!map.InBounds(nx, ny)) return;
            int n = map.Index(nx, ny);
            if (!map.Tables.BiomeFits(map.Ground[n], b)) return;
            byte nb = map.Biome[n];
            if (nb == b) return;

            bool success;
            var content = map.Content;
            if (nb == 0) success = rng.Chance(BaseChance * mul * chanceMul);
            else
            {
                int gb = content.Biomes[b - 1].GrowStrength, gn = content.Biomes[nb - 1].GrowStrength;
                if (gb == gn) success = rng.Chance(BaseChance * mul * chanceMul);
                else success = rng.Range(0, gb + 1) * chanceMul > rng.Range(0, gn + 1);
            }
            if (success) map.SetBiome(nx, ny, b, ChangeSource.Nature);
        }

        static void AgeSeeds(NatureState nature)
        {
            for (int k = nature.Seeds.Length - 1; k >= 0; k--)
            {
                var seed = nature.Seeds[k];
                if (seed.RemainingYears <= 1) nature.Seeds.RemoveAt(k);
                else
                {
                    seed.RemainingYears--;
                    nature.Seeds[k] = seed;
                }
            }
        }
    }

    // Bölüm 2.4
    public sealed class FeatureGrowthSystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.World;
        public int Order => 20;

        public void Tick(in SimContext ctx)
        {
            var map = ctx.World;
            ref var rng = ref ctx.Rng.Get(RngStream.Biome);
            int samples = NatureSampling.SamplesPerTick(map);
            int n = map.TileCount;
            for (int s = 0; s < samples; s++)
            {
                int i = rng.Range(0, n);
                FeatureGrowth.TryGrow(ctx.Nature, i % map.Width, i / map.Width, ref rng);
            }
        }
    }
}
