using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PG.WorldGen
{
    public static class WorldGenerator
    {
        const int Buckets = 1024;
        const int BatchSize = 1024;
        const float CoastRangeChamfer = 3f * 20f; // coast influence: 20 tiles
        const int MinLandSpeck = 3;

        public static WorldMap Generate(WorldGenSettings s, ContentDB db, IProgress<float> progress = null)
        {
            s.Clamp();
            if (s.Seed == 0) s.Seed = RandomSeed();
            if (db.MaxLevel < 8) throw new InvalidOperationException("WorldGen needs tile levels 0..8 (deep ocean .. summit)");

            var template = db.WorldGenTemplates.TryGet(s.Template, out var t) ? t : db.WorldGenTemplates[0];
            int size = SizeFor(s);
            var map = new WorldMap(size, size, db);
            try
            {
                using (var gen = new Run(map, db, s, template, progress)) gen.Execute();
            }
            catch
            {
                map.Dispose();
                throw;
            }
            return map;
        }

        public static int SizeFor(WorldGenSettings s)
        {
            int size = MapSizes.TilesFor(s.Size) / Math.Max(1, s.PreviewDivisor);
            return Math.Max(WorldMap.ChunkSize, (size + WorldMap.ChunkSize - 1) / WorldMap.ChunkSize * WorldMap.ChunkSize);
        }

        // Outside the simulation: picking a fresh seed for a new world is a player choice, not sim randomness.
        public static ulong RandomSeed()
        {
            ulong v = (ulong)DateTime.UtcNow.Ticks ^ ((ulong)Environment.TickCount << 32);
            v ^= v >> 33;
            v *= 0xFF51AFD7ED558CCDUL;
            v ^= v >> 33;
            return v == 0 ? 1UL : v;
        }

        sealed class Run : IDisposable
        {
            readonly WorldMap _map;
            readonly ContentDB _db;
            readonly WorldGenSettings _s;
            readonly WorldGenTemplateDef _template;
            readonly IProgress<float> _progress;
            readonly int _w, _h, _n;
            SimRandom _rng;

            NativeArray<byte> _levels;
            NativeArray<byte> _isWater;
            NativeArray<byte> _temp;
            NativeArray<float> _temperature;

            public Run(WorldMap map, ContentDB db, WorldGenSettings s, WorldGenTemplateDef template, IProgress<float> progress)
            {
                _map = map;
                _db = db;
                _s = s;
                _template = template;
                _progress = progress;
                _w = map.Width;
                _h = map.Height;
                _n = map.TileCount;
                _rng = new SimRandom(s.Seed, (ulong)RngStream.WorldGen + 1);

                _levels = new NativeArray<byte>(9, Allocator.TempJob);
                for (int l = 0; l <= 8; l++) _levels[l] = (byte)db.TileByLevel[l];
                _isWater = new NativeArray<byte>(db.Tiles.Count, Allocator.TempJob);
                for (int i = 0; i < db.Tiles.Count; i++) _isWater[i] = (byte)(db.Tiles[i].Water ? 1 : 0);
                _temp = new NativeArray<byte>(_n, Allocator.TempJob);
                _temperature = new NativeArray<float>(_n, Allocator.TempJob);
            }

            byte Deep => _levels[0];
            byte Ocean => _levels[1];
            byte Shallow => _levels[2];
            byte Sand => _levels[3];
            byte SoilLow => _levels[4];

            public void Execute()
            {
                Report(0f);
                string mask = _template.Mask?.Type ?? "none";
                switch (mask)
                {
                    case "empty":
                        Fill((byte)_template.FlatTileId);
                        ClimateAndBiomes(-1, false);
                        Report(0.6f);
                        break;
                    case "full":
                        FlatWithEdge((byte)_template.FlatTileId);
                        Coast();
                        Report(0.4f);
                        ClimateAndBiomes(_template.FlatBiomeId, true);
                        Report(0.6f);
                        break;
                    case "image":
                        ImageImporter.Apply(_s.ImagePath, _map, _db);
                        ClimateAndBiomes(-1, false);
                        Report(0.6f);
                        break;
                    default:
                        Heights();
                        Report(0.35f);
                        Cleanup();
                        Report(0.5f);
                        Coast();
                        ClimateAndBiomes(-1, true);
                        Report(0.7f);
                        Patches();
                        break;
                }

                Variants();
                Report(0.8f);
                Ores();
                Vegetation();
                Report(0.9f);
                _map.RebuildDerivedData();
                ZoneTemperatures();
                Report(1f);
            }

            // --- Step 1-3: height field, sea level percentile, leveled tiles ---
            void Heights()
            {
                var heights = new NativeArray<float>(_n, Allocator.TempJob);
                var hist = new NativeArray<int>(Buckets, Allocator.TempJob);
                var thresholds = new NativeArray<float>(8, Allocator.TempJob);
                var blobs = BuildBlobs();
                try
                {
                    var tmpl = _template;
                    new HeightJob
                    {
                        Width = _w,
                        Height = _h,
                        Octaves = math.max(1, tmpl.Noise.Octaves),
                        Frequency = 3f * tmpl.Noise.FrequencyMul,
                        Lacunarity = tmpl.Noise.Lacunarity,
                        Gain = tmpl.Noise.Gain,
                        WarpAmp = tmpl.Warp.Amplitude * _s.Roughness * 2f,
                        WarpFrequency = 3f * tmpl.Warp.FrequencyMul,
                        OffMain = RandomOffset(),
                        OffWarpX = RandomOffset(),
                        OffWarpY = RandomOffset(),
                        OffMask = RandomOffset(),
                        MaskType = MaskTypeOf(tmpl.Mask.Type),
                        Falloff = math.max(0.1f, tmpl.Mask.FalloffPower),
                        AnnulusRadius = tmpl.Mask.Radius != null && tmpl.Mask.Radius.Length > 0 ? _rng.Range(tmpl.Mask.Radius[0], tmpl.Mask.Radius[tmpl.Mask.Radius.Length - 1]) : 0.3f,
                        AnnulusWidth = math.max(0.01f, tmpl.Mask.Width),
                        EdgeOcean = tmpl.EdgeOcean,
                        Blobs = blobs,
                        Heights = heights,
                    }.Schedule(_n, BatchSize).Complete();

                    new HistogramJob { Heights = heights, Histogram = hist }.Run();

                    float water = 1f - _s.LandRatio, land = _s.LandRatio;
                    float m = _s.MountainRatio, hr = _s.HillRatio;
                    float[] fractions =
                    {
                        0.55f * water,
                        0.85f * water,
                        water,
                        water + 0.06f * land,
                        water + (1f - m - hr - 0.25f) * land,
                        water + (1f - m - hr) * land,
                        water + (1f - m) * land,
                        water + (1f - m * 0.15f) * land,
                    };
                    float prev = 0f;
                    for (int k = 0; k < fractions.Length; k++)
                    {
                        prev = math.max(prev, fractions[k]);
                        thresholds[k] = Quantile(hist, _n, prev);
                    }

                    new ClassifyJob { Heights = heights, Thresholds = thresholds, Levels = _levels, Ground = _map.Ground }
                        .Schedule(_n, BatchSize).Complete();
                }
                finally
                {
                    heights.Dispose();
                    hist.Dispose();
                    thresholds.Dispose();
                    blobs.Dispose();
                }
            }

            NativeArray<float4> BuildBlobs()
            {
                var mask = _template.Mask;
                int kind = MaskTypeOf(mask.Type);
                float r0 = mask.Radius != null && mask.Radius.Length > 0 ? mask.Radius[0] : 0.4f;
                float r1 = mask.Radius != null && mask.Radius.Length > 1 ? mask.Radius[1] : r0;

                if (kind == MaskKind.Radial)
                {
                    var one = new NativeArray<float4>(1, Allocator.TempJob);
                    one[0] = new float4(0.5f, 0.5f, _rng.Range(r0, r1), 0f);
                    return one;
                }
                if (kind != MaskKind.MultiBlob) return new NativeArray<float4>(0, Allocator.TempJob);

                int c0 = mask.Count != null && mask.Count.Length > 0 ? mask.Count[0] : 2;
                int c1 = mask.Count != null && mask.Count.Length > 1 ? mask.Count[1] : c0;
                int count = math.max(1, _rng.Range(c0, c1 + 1));
                var blobs = new NativeArray<float4>(count, Allocator.TempJob);
                for (int b = 0; b < count; b++)
                {
                    float2 p = default;
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        p = new float2(_rng.Range(0.2f, 0.8f), _rng.Range(0.2f, 0.8f));
                        bool ok = true;
                        for (int k = 0; k < b && ok; k++) ok = math.distance(p, blobs[k].xy) >= 0.3f;
                        if (ok) break;
                    }
                    blobs[b] = new float4(p, _rng.Range(r0, r1), 0f);
                }
                return blobs;
            }

            static int MaskTypeOf(string type)
            {
                switch (type)
                {
                    case "radial": return MaskKind.Radial;
                    case "multiBlob": return MaskKind.MultiBlob;
                    case "annulus": return MaskKind.Annulus;
                    case "inverse": return MaskKind.Inverse;
                    default: return MaskKind.None;
                }
            }

            // Global quantile with linear interpolation inside the bucket. frac >= 1 -> above every height.
            static float Quantile(NativeArray<int> hist, int total, float frac)
            {
                if (frac >= 1f) return 2f;
                long target = (long)(frac * total);
                long cum = 0;
                for (int b = 0; b < hist.Length; b++)
                {
                    int c = hist[b];
                    if (cum + c > target)
                    {
                        float within = c > 0 ? (target - cum) / (float)c : 0f;
                        return (b + within) / hist.Length;
                    }
                    cum += c;
                }
                return 2f;
            }

            float2 RandomOffset() => new float2(_rng.Range(-1000f, 1000f), _rng.Range(-1000f, 1000f));

            // --- Step 4: cellular cleanup and speck removal ---
            void Cleanup()
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    new SmoothJob { Width = _w, Height = _h, OutsideTile = Deep, Src = _map.Ground, Dst = _temp }
                        .Schedule(_n, BatchSize).Complete();
                    _map.Ground.CopyFrom(_temp);
                }

                var visited = new NativeArray<byte>(_n, Allocator.TempJob);
                var stack = new NativeArray<int>(_n, Allocator.TempJob);
                try
                {
                    new RemoveSpecksJob
                    {
                        Width = _w,
                        Height = _h,
                        MinSize = MinLandSpeck,
                        ReplaceWith = Shallow,
                        Ground = _map.Ground,
                        IsWaterByType = _isWater,
                        Visited = visited,
                        Stack = stack,
                    }.Run();
                }
                finally
                {
                    visited.Dispose();
                    stack.Dispose();
                }
            }

            // --- Step 5: coast ---
            void Coast()
            {
                new CoastJob
                {
                    Width = _w,
                    Height = _h,
                    Deep = Deep,
                    Ocean = Ocean,
                    Shallow = Shallow,
                    Sand = Sand,
                    SoilLow = SoilLow,
                    IsWaterByType = _isWater,
                    Src = _map.Ground,
                    Dst = _temp,
                }.Schedule(_n, BatchSize).Complete();
                _map.Ground.CopyFrom(_temp);
            }

            // --- Steps 6-7: climate and biome table (forcedBiome >= 0 paints that biome everywhere instead) ---
            void ClimateAndBiomes(int forcedBiome, bool writeBiomes)
            {
                var table = _db.BiomeTables[0];
                var grid = new NativeArray<int>(math.max(1, table.GridIds.Length), Allocator.TempJob);
                var capable = new NativeArray<byte>(_db.Tiles.Count, Allocator.TempJob);
                var levels = new NativeArray<sbyte>(_db.Tiles.Count, Allocator.TempJob);
                var dist = new NativeArray<int>(_n, Allocator.TempJob);
                try
                {
                    int rows = forcedBiome >= 0 ? 1 : table.Rows;
                    int cols = forcedBiome >= 0 ? 1 : table.Cols;
                    if (forcedBiome >= 0) grid[0] = forcedBiome + 1;
                    else
                        for (int k = 0; k < table.GridIds.Length; k++) grid[k] = table.GridIds[k] + 1;

                    for (int i = 0; i < _db.Tiles.Count; i++)
                    {
                        var tile = _db.Tiles[i];
                        capable[i] = (byte)(tile.CanHaveBiome && !tile.Water ? 1 : 0);
                        levels[i] = (sbyte)tile.Level;
                    }

                    new ChamferJob { Width = _w, Height = _h, Ground = _map.Ground, IsWaterByType = _isWater, Dist = dist }.Run();

                    new ClimateBiomeJob
                    {
                        Width = _w,
                        Height = _h,
                        MoistureShift = _s.Moisture - 0.5f,
                        TemperatureShift = _s.Temperature - 0.5f,
                        CoastRange = CoastRangeChamfer,
                        OffMoisture = RandomOffset(),
                        OffTemperature = RandomOffset(),
                        WriteBiomes = writeBiomes,
                        GridRows = rows,
                        GridCols = cols,
                        GridBiome = grid,
                        Ground = _map.Ground,
                        Dist = dist,
                        LevelByType = levels,
                        BiomeCapableLand = capable,
                        Biome = _map.Biome,
                        Temperature = _temperature,
                    }.Schedule(_n, BatchSize).Complete();
                }
                finally
                {
                    grid.Dispose();
                    capable.Dispose();
                    levels.Dispose();
                    dist.Dispose();
                }
            }

            // Special biomes grown as flood-filled patches: count = round(BiomeVariety * chunks * patchesPerChunk).
            void Patches()
            {
                var table = _db.BiomeTables[0];
                if (table.PatchIds.Length == 0) return;
                int count = (int)math.round(_s.BiomeVariety * _map.ChunkCount * table.PatchesPerChunk);
                if (count <= 0) return;

                var stamp = new int[_n];
                var queue = new int[table.PatchSize[1] * 4 + 8];
                var biome = _map.Biome;

                for (int p = 1; p <= count; p++)
                {
                    int start = -1;
                    for (int attempt = 0; attempt < 50 && start < 0; attempt++)
                    {
                        int i = _rng.Range(0, _n);
                        if (biome[i] != 0) start = i;
                    }
                    if (start < 0) continue;

                    byte patchBiome = (byte)(table.PatchIds[_rng.Range(0, table.PatchIds.Length)] + 1);
                    int target = _rng.Range(table.PatchSize[0], table.PatchSize[1] + 1);
                    int frontier = 0, painted = 0;
                    queue[frontier++] = start;
                    stamp[start] = p;
                    // Growing from a random frontier tile (not FIFO) keeps patches blobby instead of diamond-shaped.
                    while (frontier > 0 && painted < target)
                    {
                        int pick = _rng.Range(0, frontier);
                        int i = queue[pick];
                        queue[pick] = queue[--frontier];
                        biome[i] = patchBiome;
                        painted++;
                        int x = i % _w, y = i / _w;
                        for (int dir = 0; dir < 4 && frontier < queue.Length; dir++)
                        {
                            int nx = x + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
                            int ny = y + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                            if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;
                            int j = ny * _w + nx;
                            if (stamp[j] == p || biome[j] == 0) continue;
                            stamp[j] = p;
                            queue[frontier++] = j;
                        }
                    }
                }
            }

            // --- Step 8: variants ---
            void Variants()
            {
                new VariantJob { Width = _w, Seed = (uint)_s.Seed ^ (uint)(_s.Seed >> 32), Offset = RandomOffset(), Variant = _map.Variant }
                    .Schedule(_n, BatchSize).Complete();
            }

            // --- Step 9: ore veins (Poisson disk on hills/mountains) ---
            void Ores()
            {
                int featureCount = _db.Features.Count;
                var weights = new float[featureCount];
                bool any = false;
                for (int i = 0; i < featureCount; i++)
                {
                    weights[i] = _db.Features[i].VeinWeight;
                    any |= weights[i] > 0f;
                }
                if (!any) return;

                byte hills = _levels[6], mountain = _levels[7];
                float minDist = math.lerp(18f, 6f, _s.OreDensity);
                float cell = minDist / math.SQRT2;
                int gw = (int)math.ceil(_w / cell), gh = (int)math.ceil(_h / cell);
                var grid = new int[gw * gh];
                for (int i = 0; i < grid.Length; i++) grid[i] = -1;
                int attempts = (int)(_n / (minDist * minDist) * 3f);
                var ground = _map.Ground;
                var feature = _map.Feature;

                for (int a = 0; a < attempts; a++)
                {
                    int x = _rng.Range(0, _w), y = _rng.Range(0, _h);
                    byte g = ground[y * _w + x];
                    if (g != hills && g != mountain) continue;

                    int gx = (int)(x / cell), gy = (int)(y / cell);
                    bool free = true;
                    for (int dy = -2; dy <= 2 && free; dy++)
                    {
                        for (int dx = -2; dx <= 2 && free; dx++)
                        {
                            int cx = gx + dx, cy = gy + dy;
                            if (cx < 0 || cy < 0 || cx >= gw || cy >= gh) continue;
                            int other = grid[cy * gw + cx];
                            if (other < 0) continue;
                            int ox = other % _w, oy = other / _w;
                            if ((ox - x) * (ox - x) + (oy - y) * (oy - y) < minDist * minDist) free = false;
                        }
                    }
                    if (!free) continue;
                    grid[gy * gw + gx] = y * _w + x;

                    int pick = _rng.WeightedIndex(weights);
                    if (pick < 0) continue;
                    ushort value = _db.Features[pick].MapValue;
                    int length = _rng.Range(3, 10);
                    int vx = x, vy = y;
                    for (int step = 0; step < length; step++)
                    {
                        int i = vy * _w + vx;
                        byte vg = ground[i];
                        if (vg == hills || vg == mountain)
                        {
                            feature[i] = value;
                            _map.FeatureState[i] = (byte)((_map.Tables.FeatureResource[value] << 2) | 2);
                        }
                        int dir = _rng.Range(0, 4);
                        vx = math.clamp(vx + (dir == 0 ? 1 : dir == 1 ? -1 : 0), 0, _w - 1);
                        vy = math.clamp(vy + (dir == 2 ? 1 : dir == 3 ? -1 : 0), 0, _h - 1);
                    }
                }
            }

            // --- Step 10: initial trees and plants from each biome's lists, grown stages, zone caps (Bölüm 2.4) ---
            void Vegetation()
            {
                const int maxTrees = FeatureDensity.MaxTreesPerZone, maxPlants = FeatureDensity.MaxPlantsPerZone;
                float treeChance = _s.ForestDensity * 0.12f, plantChance = 0.04f + _s.ForestDensity * 0.04f;
                var trees = new byte[_map.Zones.Length];
                var plants = new byte[_map.Zones.Length];
                var tables = _map.Tables;
                for (int y = 0; y < _h; y++)
                {
                    for (int x = 0; x < _w; x++)
                    {
                        int i = y * _w + x;
                        byte b = _map.Biome[i];
                        if (b == 0 || _map.Feature[i] != 0) continue;
                        var biome = _db.Biomes[b - 1];
                        if (biome.EffectKind == BiomeEffect.NoPlants) continue;
                        int z = _map.ZoneIndexOf(x, y);
                        ushort pick = 0;
                        if (biome.TreeValues.Length > 0 && trees[z] < maxTrees && _rng.Chance(treeChance))
                        {
                            pick = biome.TreeValues[_rng.Range(0, biome.TreeValues.Length)];
                            trees[z]++;
                        }
                        else if (biome.PlantValues.Length > 0 && plants[z] < maxPlants && _rng.Chance(plantChance))
                        {
                            pick = biome.PlantValues[_rng.Range(0, biome.PlantValues.Length)];
                            plants[z]++;
                        }
                        if (pick == 0 || !tables.FeatureFits(_map.Ground[i], pick)) continue;
                        _map.Feature[i] = pick;
                        _map.FeatureState[i] = (byte)((tables.FeatureResource[pick] << 2) | _rng.Range(1, 4));
                    }
                }
            }

            void Fill(byte tile)
            {
                for (int i = 0; i < _n; i++)
                {
                    _map.Ground[i] = tile;
                    _map.Biome[i] = 0;
                }
            }

            // "full" mask: flat land with an ocean band of EdgeOcean at the map border.
            void FlatWithEdge(byte tile)
            {
                float band = _template.EdgeOcean * math.min(_w, _h);
                for (int y = 0; y < _h; y++)
                {
                    for (int x = 0; x < _w; x++)
                    {
                        float edge = math.min(math.min(x, y), math.min(_w - 1 - x, _h - 1 - y));
                        _map.Ground[y * _w + x] = edge < band ? Deep : tile;
                    }
                }
            }

            void ZoneTemperatures()
            {
                int zw = _map.ZonesX, zh = _map.ZonesY;
                for (int zy = 0; zy < zh; zy++)
                {
                    for (int zx = 0; zx < zw; zx++)
                    {
                        float sum = 0f;
                        int x0 = zx << WorldMap.ZoneShift, y0 = zy << WorldMap.ZoneShift;
                        for (int y = y0; y < y0 + WorldMap.ZoneSize; y++)
                            for (int x = x0; x < x0 + WorldMap.ZoneSize; x++)
                                sum += _temperature[y * _w + x];
                        float t = sum / (WorldMap.ZoneSize * WorldMap.ZoneSize);
                        int z = zy * zw + zx;
                        var zone = _map.Zones[z];
                        zone.BaseTemperatureC = (short)math.round(math.lerp(-20f, 40f, t));
                        zone.TemperatureC = zone.BaseTemperatureC; // refined monthly by TemperatureSystem
                        _map.Zones[z] = zone;
                    }
                }
            }

            void Report(float value) => _progress?.Report(value);

            public void Dispose()
            {
                if (_levels.IsCreated) _levels.Dispose();
                if (_isWater.IsCreated) _isWater.Dispose();
                if (_temp.IsCreated) _temp.Dispose();
                if (_temperature.IsCreated) _temperature.Dispose();
            }
        }
    }
}
