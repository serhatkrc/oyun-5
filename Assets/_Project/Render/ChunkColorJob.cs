using PG.Content;
using PG.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Render
{
    // Bölüm 1.11.2: pixel color per tile for a batch of chunks (4096 pixels each).
    // Alpha carries the shader material code: 0 land, 128 water, 255 lava.
    [BurstCompile]
    public struct ChunkColorJob : IJobParallelFor
    {
        public const int PixelsPerChunk = WorldMap.ChunkSize * WorldMap.ChunkSize;

        public int Width, Height, ChunksX;
        public bool ShowRegions;
        public Color32 Snow, Road;

        [ReadOnly] public NativeArray<int> ChunkList;
        [ReadOnly] public NativeArray<byte> Ground, Biome, Variant, Fire;
        [ReadOnly] public NativeArray<ushort> Flags;
        [ReadOnly] public NativeArray<Color32> TileColors;   // tileCount * 4
        [ReadOnly] public NativeArray<Color32> BiomeColors;  // (biomeCount + 1) * 4, slot 0 unused
        [ReadOnly] public NativeArray<Color32> FirePalette;  // 4
        [ReadOnly] public NativeArray<byte> ReliefLevel, Material;
        [ReadOnly] public NativeArray<float> ReliefShade;
        [ReadOnly] public NativeArray<int> LandRegion, WaterRegion, IslandOf;

        [WriteOnly] public NativeArray<Color32> Pixels;

        public void Execute(int p)
        {
            int k = p >> 12, local = p & (PixelsPerChunk - 1);
            int chunk = ChunkList[k];
            int x = (chunk % ChunksX) * WorldMap.ChunkSize + (local & 63);
            int y = (chunk / ChunksX) * WorldMap.ChunkSize + (local >> 6);
            int i = y * Width + x;

            byte g = Ground[i];
            int v = Variant[i] & 3;
            byte b = Biome[i];
            var c = ToF(b != 0 ? BiomeColors[b * 4 + v] : TileColors[g * 4 + v]);

            ushort f = Flags[i];
            if ((f & (ushort)TileFlags.SnowCover) != 0) c = math.lerp(c, ToF(Snow), 0.85f);
            if ((f & (ushort)TileFlags.Burning) != 0) c = ToF(FirePalette[Fire[i] >> 6]);
            if ((f & (ushort)TileFlags.Road) != 0) c = ToF(Road);

            if (y + 1 < Height)
            {
                int dNorth = ReliefLevel[g] - ReliefLevel[Ground[i + Width]];
                c.xyz *= 1f + math.clamp(dNorth, -2, 2) * ReliefShade[g];
            }

            if (ShowRegions) c = RegionTint(c, i, x, y);

            byte material = Material[g];
            c.w = material == TileTables.MaterialWater ? 128f / 255f : material == TileTables.MaterialLava ? 1f : 0f;
            c = math.saturate(c) * 255f + 0.5f;
            Pixels[p] = new Color32((byte)c.x, (byte)c.y, (byte)c.z, (byte)c.w);
        }

        float4 RegionTint(float4 c, int i, int x, int y)
        {
            int region = LandRegion[i] >= 0 ? LandRegion[i] : WaterRegion[i];
            if (region < 0) return c * 0.4f;

            uint h = math.hash(new int2(region, 7919));
            var hue = new float4((h & 255) / 255f, ((h >> 8) & 255) / 255f, ((h >> 16) & 255) / 255f, 1f);
            c = math.lerp(c, hue, 0.55f);

            // Thick island borders: darken where a neighbour belongs to another island.
            int island = Island(i);
            if ((x + 1 < Width && Island(i + 1) != island) || (y + 1 < Height && Island(i + Width) != island) ||
                (x > 0 && Island(i - 1) != island) || (y > 0 && Island(i - Width) != island))
                c.xyz *= 0.35f;
            return c;
        }

        int Island(int i)
        {
            int r = LandRegion[i] >= 0 ? LandRegion[i] : WaterRegion[i];
            return r >= 0 && r < IslandOf.Length ? IslandOf[r] : -1;
        }

        static float4 ToF(Color32 c) => new float4(c.r, c.g, c.b, c.a) / 255f;
    }
}
