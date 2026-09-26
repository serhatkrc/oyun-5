using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PG.Sim
{
    public static class TileActionKind
    {
        public const int Freeze = 0, Melt = 1, SnowOn = 2, SnowOff = 3, Evaporate = 4, LavaBubble = 5, Spark = 6;
        public const int Shift = 3; // key = tileIndex << Shift | kind
    }

    // Bölüm 2.5: monthly zone temperature + threshold actions (freeze, melt, snow, biome effects). One zone per job index.
    [BurstCompile]
    public struct ZoneClimateJob : IJobParallelFor
    {
        public int Width, ZonesX, ZonesY;
        public float TempShift, SeasonAmp, SeasonConst;
        public bool YearStart;
        public ulong Seed;
        public long Tick;

        [ReadOnly] public NativeArray<byte> Ground, Biome;
        [ReadOnly] public NativeArray<ushort> Flags;
        [ReadOnly] public NativeArray<byte> ZoneAsh;
        [ReadOnly] public NativeArray<sbyte> ZoneHeat, BiomeTempOffset;
        [ReadOnly] public NativeArray<byte> FreezeTo, MeltTo, BiomeSnowCover, BiomeEffectCode;
        [ReadOnly] public NativeArray<ushort> TypeFlags;

        public NativeArray<ZoneData> Zones;
        public NativeList<int>.ParallelWriter Actions;

        public void Execute(int z)
        {
            int zx = z % ZonesX, zy = z / ZonesX;
            int x0 = zx << WorldMap.ZoneShift, y0 = zy << WorldMap.ZoneShift;
            var counts = new FixedList128Bytes<int2>(); // (biome, count), at most 15 distinct biomes per zone
            int heat = 0, burning = 0;

            for (int dy = 0; dy < WorldMap.ZoneSize; dy++)
            for (int dx = 0; dx < WorldMap.ZoneSize; dx++)
            {
                int i = (y0 + dy) * Width + x0 + dx;
                heat = math.max(heat, ZoneHeat[Ground[i]]);
                if ((Flags[i] & (ushort)TileFlags.Burning) != 0) burning++;
                byte b = Biome[i];
                if (b == 0) continue;
                bool found = false;
                for (int k = 0; k < counts.Length; k++)
                    if (counts[k].x == b) { counts[k] = new int2(b, counts[k].y + 1); found = true; break; }
                if (!found && counts.Length < counts.Capacity) counts.Add(new int2(b, 1));
            }

            byte dominant = 0;
            int best = 0;
            for (int k = 0; k < counts.Length; k++)
                if (counts[k].y > best) { best = counts[k].y; dominant = (byte)counts[k].x; }

            // Latitude: 0.3 at the equator (map middle) to 1 at the poles.
            float lat = math.abs((zy + 0.5f) / ZonesY - 0.5f) * 2f;
            float season = SeasonConst + SeasonAmp * math.lerp(0.3f, 1f, lat);

            var zone = Zones[z];
            float temp = zone.BaseTemperatureC + TempShift + season + BiomeTempOffset[dominant] + heat
                         + math.min(burning * 0.2f, 10f) - (ZoneAsh[z] > 0 ? 5f : 0f);
            zone.TemperatureC = (short)math.clamp((int)math.round(temp), -60, 80);
            zone.DominantBiome = dominant;
            Zones[z] = zone;
            float t = zone.TemperatureC;

            var rng = SimRandom.Derive(Seed, z, Tick);
            for (int dy = 0; dy < WorldMap.ZoneSize; dy++)
            for (int dx = 0; dx < WorldMap.ZoneSize; dx++)
            {
                int i = (y0 + dy) * Width + x0 + dx;
                byte g = Ground[i];
                ushort f = Flags[i];
                bool water = (TypeFlags[g] & (ushort)TileFlags.Water) != 0;
                if (t < -5f && FreezeTo[g] != TileTables.NoStep && rng.Chance(0.2f)) Emit(i, TileActionKind.Freeze);
                else if (t > 2f && MeltTo[g] != TileTables.NoStep && rng.Chance(0.3f)) Emit(i, TileActionKind.Melt);

                bool snow = (f & (ushort)TileFlags.SnowCover) != 0;
                if (!snow && !water && t < 0f && BiomeSnowCover[Biome[i]] != 0) Emit(i, TileActionKind.SnowOn);
                else if (snow && t > 5f && rng.Chance(0.5f)) Emit(i, TileActionKind.SnowOff);

                if (YearStart && water && (TypeFlags[g] & (ushort)TileFlags.Walkable) != 0 && NearDesert(i, x0 + dx, y0 + dy) && rng.Chance(0.03f))
                    Emit(i, TileActionKind.Evaporate);
            }

            byte code = BiomeEffectCode[dominant];
            int pick = (y0 + rng.Range(0, WorldMap.ZoneSize)) * Width + x0 + rng.Range(0, WorldMap.ZoneSize);
            if (code == (byte)BiomeEffect.RandomFire && rng.Chance(0.02f)) Emit(pick, TileActionKind.Spark);
            if (YearStart && code == (byte)BiomeEffect.LavaBubble && rng.Chance(0.01f)) Emit(pick, TileActionKind.LavaBubble);
        }

        bool NearDesert(int i, int x, int y)
        {
            int height = Ground.Length / Width;
            return (x > 0 && IsDesert(i - 1)) || (x + 1 < Width && IsDesert(i + 1)) ||
                   (y > 0 && IsDesert(i - Width)) || (y + 1 < height && IsDesert(i + Width));
        }

        bool IsDesert(int i) => BiomeEffectCode[Biome[i]] == (byte)BiomeEffect.Evaporate;

        void Emit(int i, int kind) => Actions.AddNoResize((i << TileActionKind.Shift) | kind);
    }

    public sealed class TemperatureSystem : ISimSystem, IDisposable
    {
        public const float SeasonAmplitude = 10f, EternalSummer = 10f, EternalWinter = -15f;

        NativeList<int> _actions;
        readonly byte _sand, _lava;

        public TemperatureSystem(ContentDB content)
        {
            _sand = (byte)content.Tiles.IdOrDefault("tile.sand", content.TileByLevel[3]);
            _lava = (byte)content.Tiles.IdOrDefault("tile.lava_hot", 0);
        }

        public SimPhase Phase => SimPhase.Climate;
        public int Order => 0;

        public void Tick(in SimContext ctx)
        {
            if (!ctx.Clock.IsMonthStart) return;
            Recompute(ctx.World, ctx.Nature, ctx.Clock, ctx.Rng.WorldSeed, true);
        }

        // apply = false: temperatures only (worldgen / load preview), no tile actions.
        public void Recompute(WorldMap map, NatureState nature, GameClock clock, ulong seed, bool apply)
        {
            var t = map.Tables;
            float seasonAmp = 0f, seasonConst = 0f;
            if (nature.Laws.IsOn(nature.LawEternalSummer)) seasonConst = EternalSummer;
            else if (nature.Laws.IsOn(nature.LawEternalWinter)) seasonConst = EternalWinter;
            else if (nature.Laws.IsOn(nature.LawSeasons)) seasonAmp = SeasonAmplitude * math.sin(2f * math.PI * clock.Month / 12f);

            int capacity = map.TileCount * 2 + map.Zones.Length * 2; // <= 2 actions per tile + 2 per zone
            if (!_actions.IsCreated) _actions = new NativeList<int>(capacity, Allocator.Persistent);
            _actions.Clear();
            if (_actions.Capacity < capacity) _actions.Capacity = capacity;

            new ZoneClimateJob
            {
                Width = map.Width, ZonesX = map.ZonesX, ZonesY = map.ZonesY,
                TempShift = nature.Mods.TempShift, SeasonAmp = seasonAmp, SeasonConst = seasonConst,
                YearStart = clock.IsYearStart, Seed = seed, Tick = clock.Tick,
                Ground = map.Ground, Biome = map.Biome, Flags = map.Flags, ZoneAsh = nature.ZoneAshMonths,
                ZoneHeat = t.ZoneHeat, BiomeTempOffset = t.BiomeTempOffset, FreezeTo = t.FreezeTo, MeltTo = t.MeltTo,
                BiomeSnowCover = t.BiomeSnowCover, BiomeEffectCode = t.BiomeEffectCode, TypeFlags = t.TypeFlags,
                Zones = map.Zones, Actions = _actions.AsParallelWriter(),
            }.Schedule(map.Zones.Length, 16).Complete();

            for (int z = 0; z < nature.ZoneAshMonths.Length; z++)
                if (nature.ZoneAshMonths[z] > 0) nature.ZoneAshMonths[z]--;

            if (!apply || _actions.Length == 0) return;
            _actions.Sort(); // parallel output order is not deterministic; tile order is
            using (map.BeginBatch(ChangeSource.Nature))
            {
                for (int k = 0; k < _actions.Length; k++)
                {
                    int key = _actions[k];
                    int i = key >> TileActionKind.Shift;
                    int x = i % map.Width, y = i / map.Width;
                    byte g = map.Ground[i];
                    switch (key & 7)
                    {
                        case TileActionKind.Freeze: nature.Freeze(x, y); break;
                        case TileActionKind.Melt:
                            if (t.MeltTo[g] != TileTables.NoStep) map.SetGround(x, y, t.MeltTo[g], ChangeSource.Nature);
                            break;
                        case TileActionKind.SnowOn: map.SetFlag(x, y, TileFlags.SnowCover, true); break;
                        case TileActionKind.SnowOff: map.SetFlag(x, y, TileFlags.SnowCover, false); break;
                        case TileActionKind.Evaporate: map.SetGround(x, y, _sand, ChangeSource.Nature); break;
                        case TileActionKind.LavaBubble:
                            if (!map.IsWater(x, y)) map.SetGround(x, y, _lava, ChangeSource.Nature);
                            break;
                        case TileActionKind.Spark: nature.Ignite(x, y, NatureState.IgniteDefault, true); break;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_actions.IsCreated) _actions.Dispose();
        }
    }

    // Bölüm 2.6
    public sealed class WindSystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.Climate;
        public int Order => 10;

        public void Tick(in SimContext ctx)
        {
            if (!ctx.Clock.IsMonthStart) return;
            var nature = ctx.Nature;
            ref var rng = ref ctx.Rng.Get(RngStream.Weather);
            nature.WindAngle += math.radians(rng.Range(-20f, 20f));
            nature.WindAngle = math.fmod(nature.WindAngle + 2f * math.PI, 2f * math.PI);
            nature.WindSpeed = math.clamp(nature.WindSpeed + rng.Range(-0.15f, 0.15f), 0.2f, 0.8f);
        }
    }

    // Bölüm 2.7
    public sealed class CloudSystem : ISimSystem
    {
        public const float MoveFactor = 0.05f;
        public const int DropEvery = 4, GlobalRainDrops = 32;
        public const byte LightningFire = 200;

        readonly int[] _naturalTypes;   // natural non-ash, non-cold clouds
        readonly float[] _naturalWeights;
        readonly int _snow, _ash, _rain;

        public CloudSystem(ContentDB content)
        {
            _snow = _ash = _rain = -1;
            int n = 0;
            for (int i = 0; i < content.Clouds.Count; i++)
            {
                var c = content.Clouds[i];
                if (c.ColdOnly && _snow < 0) _snow = i;
                if (c.EffectKind == CloudEffect.Ash) _ash = i;
                if (c.EffectKind == CloudEffect.Rain && _rain < 0) _rain = i;
                if (c.NaturalWeight > 0f && !c.ColdOnly && c.EffectKind != CloudEffect.Ash) n++;
            }
            _naturalTypes = new int[n];
            _naturalWeights = new float[n];
            n = 0;
            for (int i = 0; i < content.Clouds.Count; i++)
            {
                var c = content.Clouds[i];
                if (c.NaturalWeight > 0f && !c.ColdOnly && c.EffectKind != CloudEffect.Ash)
                {
                    _naturalTypes[n] = i;
                    _naturalWeights[n++] = c.NaturalWeight;
                }
            }
        }

        public SimPhase Phase => SimPhase.Climate;
        public int Order => 20;

        public void Tick(in SimContext ctx)
        {
            var nature = ctx.Nature;
            var map = ctx.World;
            ref var rng = ref ctx.Rng.Get(RngStream.Weather);
            long tick = ctx.Clock.Tick;

            if (ctx.Clock.IsMonthStart) SpawnNatural(nature, map, tick / SimConst.TicksPerMonth, ref rng);

            using (map.BeginBatch(ChangeSource.Nature))
            {
                float2 step = nature.Wind * MoveFactor;
                var clouds = nature.Clouds;
                for (int k = clouds.Length - 1; k >= 0; k--)
                {
                    var c = clouds[k];
                    c.Pos += step;
                    c.LifetimeTicks--;
                    if (c.LifetimeTicks <= 0 || c.Pos.x < -c.Radius || c.Pos.y < -c.Radius ||
                        c.Pos.x > map.Width + c.Radius || c.Pos.y > map.Height + c.Radius)
                    {
                        clouds.RemoveAt(k); // keeps order (determinism of later iteration)
                        continue;
                    }
                    if (tick >= c.NextDropTick)
                    {
                        c.NextDropTick = (int)(tick + DropEvery);
                        Drops(nature, map, c, ref rng);
                    }
                    clouds[k] = c;
                }

                if (nature.Mods.GlobalRain)
                    for (int s = 0; s < GlobalRainDrops; s++)
                    {
                        int i = rng.Range(0, map.TileCount);
                        nature.RainDrop(i % map.Width, i / map.Width, ref rng);
                    }
            }
        }

        void SpawnNatural(NatureState nature, WorldMap map, long month, ref SimRandom rng)
        {
            if (month < nature.NextCloudMonth) return;
            nature.NextCloudMonth = month + nature.Mods.CloudIntervalMonths;
            if (!nature.Laws.IsOn(nature.LawClouds)) return;

            // A random water zone (a few tries; dry maps simply get no clouds this time).
            int zone = -1;
            for (int tries = 0; tries < 32 && zone < 0; tries++)
            {
                int z = rng.Range(0, map.Zones.Length);
                if (map.Zones[z].WaterTiles >= WorldMap.ZoneSize * WorldMap.ZoneSize / 2) zone = z;
            }
            if (zone < 0) return;

            float2 center = new float2((zone % map.ZonesX + 0.5f) * WorldMap.ZoneSize, (zone / map.ZonesX + 0.5f) * WorldMap.ZoneSize);
            bool cold = map.Zones[zone].TemperatureC < 0;
            int count = rng.Range(1, 4);
            for (int n = 0; n < count; n++)
            {
                int type;
                if (cold && _snow >= 0) type = _snow;
                else if (_ash >= 0 && rng.Chance(nature.Mods.AshCloudChance)) type = _ash;
                else
                {
                    int w = rng.WeightedIndex(_naturalWeights);
                    type = w >= 0 ? _naturalTypes[w] : _rain;
                }
                if (type < 0) continue;
                nature.SpawnCloud(type, center + new float2(rng.Range(-8f, 8f), rng.Range(-8f, 8f)), ref rng);
            }
        }

        static void Drops(NatureState nature, WorldMap map, in CloudData c, ref SimRandom rng)
        {
            var def = nature.Content.Clouds[c.Type];
            int count = math.max(1, (int)(c.Radius * 0.6f));
            for (int d = 0; d < count; d++)
            {
                float a = rng.Range(0f, 2f * math.PI), r = c.Radius * math.sqrt(rng.Value01());
                int x = (int)math.floor(c.Pos.x + math.cos(a) * r), y = (int)math.floor(c.Pos.y + math.sin(a) * r);
                if (!map.InBounds(x, y)) continue;
                int i = map.Index(x, y);
                switch (def.EffectKind)
                {
                    case CloudEffect.Rain: nature.RainDrop(x, y, ref rng); break;
                    case CloudEffect.Snow: nature.SnowDrop(x, y, ref rng); break;
                    case CloudEffect.Acid:
                        if (rng.Chance(def.DropChance))
                        {
                            map.SetFeature(x, y, 0, ChangeSource.Nature);
                            map.LowerLevel(x, y, ChangeSource.Nature);
                        }
                        break;
                    case CloudEffect.Lava:
                        if (def.DropTileId >= 0 && !map.IsWater(x, y) && rng.Chance(def.DropChance))
                            map.SetGround(x, y, (byte)def.DropTileId, ChangeSource.Nature);
                        break;
                    case CloudEffect.Storm:
                        if (rng.Chance(def.DropChance)) nature.Ignite(x, y, LightningFire, !map.IsWater(x, y));
                        break;
                    case CloudEffect.Ash:
                        int z = map.ZoneIndexOf(x, y);
                        if (nature.ZoneAshMonths[z] == 0)
                        {
                            nature.ZoneAshMonths[z] = 1;
                            var zone = map.Zones[z];
                            zone.TemperatureC -= 5;
                            map.Zones[z] = zone;
                        }
                        break;
                    case CloudEffect.Candy:
                        if (def.DropFeatureValue != 0 && map.Feature[i] == 0 && rng.Chance(def.DropChance))
                            map.SetFeature(x, y, def.DropFeatureValue, 0, ChangeSource.Nature);
                        break;
                    // Life, Blessing, Plague, Rot act on units (Bölüm 3-4).
                }
            }
        }
    }
}
