using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PG.WorldGen
{
    public static class NoiseUtil
    {
        // Fractal sum of simplex noise, normalized to -1..1.
        public static float Fbm(float2 p, int octaves, float lacunarity, float gain, float2 offset)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * noise.snoise(p + offset + new float2(o * 31.7f, o * 17.3f));
                norm += amp;
                amp *= gain;
                p *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        public static uint Hash(int x, int y, uint seed)
        {
            uint h = seed ^ 0x9E3779B9u;
            h ^= (uint)x * 0x85EBCA6Bu;
            h = math.rol(h, 13) * 0xC2B2AE35u;
            h ^= (uint)y * 0x27D4EB2Fu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            return h;
        }
    }

    public static class MaskKind
    {
        public const int None = 0, Radial = 1, MultiBlob = 2, Annulus = 3, Inverse = 4;
    }

    // Step 1: base height = FBM(warped uv) * mask * edge fade.
    [BurstCompile]
    public struct HeightJob : IJobParallelFor
    {
        public int Width, Height;
        public int Octaves;
        public float Frequency, Lacunarity, Gain;
        public float WarpAmp, WarpFrequency;
        public float2 OffMain, OffWarpX, OffWarpY, OffMask;
        public int MaskType;
        public float Falloff, AnnulusRadius, AnnulusWidth, EdgeOcean;
        [ReadOnly] public NativeArray<float4> Blobs; // xy center, z radius
        [WriteOnly] public NativeArray<float> Heights;

        public void Execute(int i)
        {
            int x = i % Width, y = i / Width;
            var uv = new float2((x + 0.5f) / Width, (y + 0.5f) / Height);

            float wx = NoiseUtil.Fbm(uv * WarpFrequency, 3, 2f, 0.5f, OffWarpX);
            float wy = NoiseUtil.Fbm(uv * WarpFrequency, 3, 2f, 0.5f, OffWarpY);
            float2 w = uv + WarpAmp * new float2(wx, wy);

            float h = NoiseUtil.Fbm(w * Frequency, Octaves, Lacunarity, Gain, OffMain) * 0.5f + 0.5f;
            h *= Mask(w);

            float edge = math.min(math.min(uv.x, uv.y), math.min(1f - uv.x, 1f - uv.y));
            if (EdgeOcean > 0f) h *= math.smoothstep(0f, EdgeOcean, edge);
            Heights[i] = math.saturate(h);
        }

        float Mask(float2 p)
        {
            switch (MaskType)
            {
                case MaskKind.Radial:
                case MaskKind.MultiBlob:
                {
                    float m = 0f;
                    for (int b = 0; b < Blobs.Length; b++)
                    {
                        float4 blob = Blobs[b];
                        float d = math.distance(p, blob.xy);
                        m = math.max(m, 1f - math.pow(math.smoothstep(0f, blob.z, d), Falloff));
                    }
                    return m;
                }
                case MaskKind.Annulus:
                {
                    float d = math.distance(p, new float2(0.5f, 0.5f));
                    return math.smoothstep(0f, 1f, math.saturate(1f - math.abs(d - AnnulusRadius) / AnnulusWidth));
                }
                case MaskKind.Inverse:
                {
                    float n = NoiseUtil.Fbm(p * 2.2f, 3, 2f, 0.5f, OffMask) * 0.5f + 0.5f;
                    return 1f - 0.6f * n;
                }
                default:
                    return 1f;
            }
        }
    }

    [BurstCompile]
    public struct HistogramJob : IJob
    {
        [ReadOnly] public NativeArray<float> Heights;
        public NativeArray<int> Histogram;

        public void Execute()
        {
            int buckets = Histogram.Length;
            for (int b = 0; b < buckets; b++) Histogram[b] = 0;
            for (int i = 0; i < Heights.Length; i++)
            {
                int b = math.clamp((int)(Heights[i] * buckets), 0, buckets - 1);
                Histogram[b]++;
            }
        }
    }

    // Step 3: heights -> leveled tile ids via percentile thresholds.
    [BurstCompile]
    public struct ClassifyJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights;
        [ReadOnly] public NativeArray<float> Thresholds; // ascending, one per level boundary (Levels.Length - 1)
        [ReadOnly] public NativeArray<byte> Levels;      // tile id per level
        [WriteOnly] public NativeArray<byte> Ground;

        public void Execute(int i)
        {
            float h = Heights[i];
            int level = 0;
            while (level < Thresholds.Length && h >= Thresholds[level]) level++;
            Ground[i] = Levels[level];
        }
    }

    // Step 4: cellular cleanup. A tile flips to a type that holds >= 6 of its 8 neighbours.
    [BurstCompile]
    public struct SmoothJob : IJobParallelFor
    {
        public int Width, Height;
        public byte OutsideTile;
        [ReadOnly] public NativeArray<byte> Src;
        [WriteOnly] public NativeArray<byte> Dst;

        public void Execute(int i)
        {
            int x = i % Width, y = i / Width;
            byte self = Src[i];
            var nb = new FixedList32Bytes<byte>();
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    nb.Add(nx < 0 || ny < 0 || nx >= Width || ny >= Height ? OutsideTile : Src[ny * Width + nx]);
                }
            }

            byte best = self;
            int bestCount = 0;
            for (int a = 0; a < nb.Length; a++)
            {
                byte v = nb[a];
                if (v == self) continue;
                int c = 0;
                for (int b = 0; b < nb.Length; b++)
                    if (nb[b] == v) c++;
                if (c > bestCount)
                {
                    bestCount = c;
                    best = v;
                }
            }
            Dst[i] = bestCount >= 6 ? best : self;
        }
    }

    // Step 4b: land specks smaller than MinSize tiles become shallow water.
    [BurstCompile]
    public struct RemoveSpecksJob : IJob
    {
        public int Width, Height, MinSize;
        public byte ReplaceWith;
        public NativeArray<byte> Ground;
        [ReadOnly] public NativeArray<byte> IsWaterByType;
        public NativeArray<byte> Visited;
        public NativeArray<int> Stack;

        public void Execute()
        {
            int n = Width * Height;
            for (int i = 0; i < n; i++) Visited[i] = 0;
            var first = new FixedList64Bytes<int>();

            for (int start = 0; start < n; start++)
            {
                if (Visited[start] != 0 || IsWaterByType[Ground[start]] != 0) continue;
                first.Clear();
                int top = 0, size = 0;
                Stack[top++] = start;
                Visited[start] = 1;
                while (top > 0)
                {
                    int i = Stack[--top];
                    if (first.Length < MinSize) first.Add(i);
                    size++;
                    int x = i % Width, y = i / Width;
                    if (x > 0) Push(i - 1, ref top);
                    if (x < Width - 1) Push(i + 1, ref top);
                    if (y > 0) Push(i - Width, ref top);
                    if (y < Height - 1) Push(i + Width, ref top);
                }
                if (size < MinSize)
                    for (int k = 0; k < first.Length; k++) Ground[first[k]] = ReplaceWith;
            }
        }

        void Push(int i, ref int top)
        {
            if (Visited[i] != 0 || IsWaterByType[Ground[i]] != 0) return;
            Visited[i] = 1;
            Stack[top++] = i;
        }
    }

    // Step 5: shallow ring between land and open water; sand only where it touches water.
    [BurstCompile]
    public struct CoastJob : IJobParallelFor
    {
        public int Width, Height;
        public byte Deep, Ocean, Shallow, Sand, SoilLow;
        [ReadOnly] public NativeArray<byte> IsWaterByType;
        [ReadOnly] public NativeArray<byte> Src;
        [WriteOnly] public NativeArray<byte> Dst;

        public void Execute(int i)
        {
            int x = i % Width, y = i / Width;
            byte t = Src[i];
            bool land = false, water = false;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) { water = true; continue; }
                    if (IsWaterByType[Src[ny * Width + nx]] != 0) water = true;
                    else land = true;
                }
            }

            if ((t == Deep || t == Ocean) && land) Dst[i] = Shallow;
            else if (t == Sand && !water) Dst[i] = SoilLow;
            else Dst[i] = t;
        }
    }

    // Distance (chamfer 3-4) from water for coast proximity.
    [BurstCompile]
    public struct ChamferJob : IJob
    {
        public int Width, Height;
        [ReadOnly] public NativeArray<byte> Ground;
        [ReadOnly] public NativeArray<byte> IsWaterByType;
        public NativeArray<int> Dist;

        public void Execute()
        {
            const int Inf = 1 << 28;
            int n = Width * Height;
            for (int i = 0; i < n; i++) Dist[i] = IsWaterByType[Ground[i]] != 0 ? 0 : Inf;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x, d = Dist[i];
                    if (x > 0) d = math.min(d, Dist[i - 1] + 3);
                    if (y > 0)
                    {
                        d = math.min(d, Dist[i - Width] + 3);
                        if (x > 0) d = math.min(d, Dist[i - Width - 1] + 4);
                        if (x < Width - 1) d = math.min(d, Dist[i - Width + 1] + 4);
                    }
                    Dist[i] = d;
                }
            }

            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = Width - 1; x >= 0; x--)
                {
                    int i = y * Width + x, d = Dist[i];
                    if (x < Width - 1) d = math.min(d, Dist[i + 1] + 3);
                    if (y < Height - 1)
                    {
                        d = math.min(d, Dist[i + Width] + 3);
                        if (x < Width - 1) d = math.min(d, Dist[i + Width + 1] + 4);
                        if (x > 0) d = math.min(d, Dist[i + Width - 1] + 4);
                    }
                    Dist[i] = d;
                }
            }
        }
    }

    // Steps 6-7: moisture/temperature fields and table biome per land tile.
    [BurstCompile]
    public struct ClimateBiomeJob : IJobParallelFor
    {
        public int Width, Height;
        public float MoistureShift, TemperatureShift, CoastRange;
        public float2 OffMoisture, OffTemperature;
        public bool WriteBiomes;
        public int GridRows, GridCols;
        [ReadOnly] public NativeArray<int> GridBiome;     // biome map value per cell (row-major)
        [ReadOnly] public NativeArray<byte> Ground;
        [ReadOnly] public NativeArray<int> Dist;
        [ReadOnly] public NativeArray<sbyte> LevelByType;
        [ReadOnly] public NativeArray<byte> BiomeCapableLand; // 1 = land tile that can hold a biome
        [NativeDisableParallelForRestriction] public NativeArray<byte> Biome;
        [WriteOnly] public NativeArray<float> Temperature;

        public void Execute(int i)
        {
            int x = i % Width, y = i / Width;
            var uv = new float2((x + 0.5f) / Width, (y + 0.5f) / Height);
            byte g = Ground[i];

            float coast = math.saturate(1f - Dist[i] / CoastRange);
            float moisture = NoiseUtil.Fbm(uv * 2f, 4, 2f, 0.5f, OffMoisture) * 0.5f + 0.5f + 0.25f * coast + MoistureShift;
            moisture = math.saturate(moisture);

            int level = LevelByType[g];
            float t = 1f - math.abs(uv.y - 0.5f) * 1.6f
                      - 0.35f * math.max(0, level - 4) / 4f
                      + 0.15f * NoiseUtil.Fbm(uv * 3f, 3, 2f, 0.5f, OffTemperature)
                      + TemperatureShift;
            t = math.saturate(t);
            Temperature[i] = t;

            if (!WriteBiomes) return;
            if (BiomeCapableLand[g] == 0)
            {
                Biome[i] = 0;
                return;
            }
            int row = math.min(GridRows - 1, (int)(t * GridRows));
            int col = math.min(GridCols - 1, (int)(moisture * GridCols));
            Biome[i] = (byte)GridBiome[row * GridCols + col];
        }
    }

    // Step 8: clustered color variants.
    [BurstCompile]
    public struct VariantJob : IJobParallelFor
    {
        public int Width;
        public uint Seed;
        public float2 Offset;
        [WriteOnly] public NativeArray<byte> Variant;

        public void Execute(int i)
        {
            int x = i % Width, y = i / Width;
            float h = (NoiseUtil.Hash(x, y, Seed) & 0xFFFF) / 65535f;
            float n = noise.snoise(new float2(x, y) * 0.09f + Offset) * 0.5f + 0.5f;
            Variant[i] = (byte)math.min(3, (int)(math.saturate(n * 0.7f + h * 0.3f) * 4f));
        }
    }
}
