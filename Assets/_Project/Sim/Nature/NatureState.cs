using System;
using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 2.2: one global struct every nature system reads. Updated by the era system (month start + era change).
    public struct NatureModifiers
    {
        public float TempShift;
        public float BiomeGrowthMul;
        public float FireSpreadMul;
        public float PlantGrowthMul;
        public int CloudIntervalMonths;
        public bool GlobalRain;
        public float FertilityMul;   // read by Bölüm 3
        public float AshCloudChance;

        public static NatureModifiers From(EraDef era)
        {
            return new NatureModifiers
            {
                TempShift = era.TempShift,
                BiomeGrowthMul = era.StopsBiomeGrowth ? 0f : math.max(0f, 1f + era.BiomeGrowthBonus * 0.25f),
                FireSpreadMul = era.FireSpreadMul,
                PlantGrowthMul = era.PlantGrowthMul,
                CloudIntervalMonths = math.max(1, era.CloudIntervalMonths),
                GlobalRain = era.GlobalRain,
                FertilityMul = era.FertilityPct / 100f,
                AshCloudChance = era.AshCloudChance,
            };
        }
    }

    public struct CloudData
    {
        public float2 Pos;
        public float Radius;        // 6-14 tiles
        public ushort Type;         // CloudDef index
        public int LifetimeTicks;   // remaining
        public int NextDropTick;
    }

    public struct SeedData
    {
        public int Tile;
        public byte Biome;          // map value
        public byte RemainingYears;
    }

    public struct LavaCell
    {
        public int Tile;
        public long DueTick;        // next decay step
        public byte Type;           // ground type the timer belongs to
        public byte BaseLevel;      // relief level of the ground the lava covered (flow goes below it)
    }

    // Bölüm 2.12
    public sealed class EraState
    {
        public const int SlotCount = 8;

        public int CurrentEra;
        public int PreviousEra;
        public int StartedYear;
        public int DurationYears;
        public readonly int[] ClockSlots = new int[SlotCount]; // era index + 1, 0 = empty slot
        public int CurrentSlot;
        public bool[] Enabled;
        public bool Frozen;
        public float TintBlend = 1f; // 0..1 from PreviousEra tint to CurrentEra tint
    }

    public static class LawIds
    {
        public const string BiomeSpread = "law.biome_spread";
        public const string TreeGrowth = "law.tree_growth";
        public const string FireSpread = "law.fire_spread";
        public const string Clouds = "law.clouds";
        public const string AutoDisasters = "law.auto_disasters";
        public const string Seasons = "law.seasons";
        public const string LavaCooling = "law.lava_cooling";
        public const string GooSpread = "law.goo_spread";
        public const string EternalSummer = "law.eternal_summer";
        public const string EternalWinter = "law.eternal_winter";
    }

    // World laws (world_laws.json): toggles and optional slider values, indexed by WorldLawDef.Index.
    public sealed class WorldLaws
    {
        readonly ContentDB _db;
        readonly bool[] _on;
        readonly float[] _value;

        public WorldLaws(ContentDB db)
        {
            _db = db;
            _on = new bool[db.Laws.Count];
            _value = new float[db.Laws.Count];
            ResetToDefaults();
        }

        public int Count => _on.Length;

        public void ResetToDefaults()
        {
            for (int i = 0; i < _on.Length; i++)
            {
                _on[i] = _db.Laws[i].Default;
                _value[i] = _db.Laws[i].SliderDefault;
            }
        }

        public int IndexOf(string id) => _db.Laws.IdOrDefault(id);
        public bool IsOn(int law) => law >= 0 && _on[law];
        public float Value(int law) => law >= 0 ? _value[law] : 0f;
        public bool IsOn(string id) => IsOn(IndexOf(id));

        public void Set(int law, bool on, float value)
        {
            if (law < 0 || law >= _on.Length) return;
            _on[law] = on;
            var def = _db.Laws[law];
            _value[law] = def.HasSlider ? math.clamp(value, def.Slider[0], def.Slider[1]) : 0f;
        }
    }

    // All nature bookkeeping that is not stored per tile in WorldMap. Owned by SimWorld.
    public sealed class NatureState : IDisposable
    {
        public const int MaxClouds = 64;
        public const int MaxBurning = 50000;
        public const int MaxSeeds = 256;
        public const byte IgniteDefault = 80;

        public readonly WorldMap World;
        public readonly ContentDB Content;
        public readonly WorldLaws Laws;
        public readonly EraState Era = new EraState();

        public NatureModifiers Mods;
        public float WindAngle;         // radians
        public float WindSpeed = 0.5f;  // 0.2-0.8
        public long NextCloudMonth;     // absolute month index of the next natural cloud spawn
        public int[] DisasterLastYear;  // per DisasterDef, int.MinValue = never

        public NativeList<CloudData> Clouds;
        public NativeList<SeedData> Seeds;
        public NativeList<int> Burning;     // membership = TileFlags.Burning (entries without the flag are stale)
        public NativeList<LavaCell> Lava;
        public NativeList<int> Goo;
        public NativeArray<byte> ZoneAshMonths;

        // Cached law indices
        public readonly int LawBiomeSpread, LawTreeGrowth, LawFireSpread, LawClouds, LawAutoDisasters,
                            LawSeasons, LawLavaCooling, LawGooSpread, LawEternalSummer, LawEternalWinter;

        public NatureState(WorldMap world, ContentDB content, ulong worldSeed)
        {
            World = world;
            Content = content;
            Laws = new WorldLaws(content);
            LawBiomeSpread = Laws.IndexOf(LawIds.BiomeSpread);
            LawTreeGrowth = Laws.IndexOf(LawIds.TreeGrowth);
            LawFireSpread = Laws.IndexOf(LawIds.FireSpread);
            LawClouds = Laws.IndexOf(LawIds.Clouds);
            LawAutoDisasters = Laws.IndexOf(LawIds.AutoDisasters);
            LawSeasons = Laws.IndexOf(LawIds.Seasons);
            LawLavaCooling = Laws.IndexOf(LawIds.LavaCooling);
            LawGooSpread = Laws.IndexOf(LawIds.GooSpread);
            LawEternalSummer = Laws.IndexOf(LawIds.EternalSummer);
            LawEternalWinter = Laws.IndexOf(LawIds.EternalWinter);

            Clouds = new NativeList<CloudData>(MaxClouds, Allocator.Persistent);
            Seeds = new NativeList<SeedData>(16, Allocator.Persistent);
            Burning = new NativeList<int>(1024, Allocator.Persistent);
            Lava = new NativeList<LavaCell>(256, Allocator.Persistent);
            Goo = new NativeList<int>(64, Allocator.Persistent);
            ZoneAshMonths = new NativeArray<byte>(world.Zones.Length, Allocator.Persistent);

            DisasterLastYear = new int[content.Disasters.Count];
            for (int i = 0; i < DisasterLastYear.Length; i++) DisasterLastYear[i] = int.MinValue;

            // A derived generator keeps the shared streams untouched (a loaded game restores them afterwards).
            var rng = SimRandom.Derive(worldSeed, 0x0E7A, 0);
            WindAngle = rng.Range(0f, 2f * math.PI);
            SetupDefaultEraClock(ref rng);
            RebuildFromMap();
        }

        public float2 Wind => new float2(math.cos(WindAngle), math.sin(WindAngle)) * WindSpeed;
        public EraDef CurrentEra => Content.Eras[Era.CurrentEra];

        void SetupDefaultEraClock(ref SimRandom rng)
        {
            int n = Content.Eras.Count;
            Era.Enabled = new bool[n];
            for (int i = 0; i < n; i++)
            {
                Era.Enabled[i] = true;
                int slot = Content.Eras[i].DefaultSlot;
                if (slot >= 0 && slot < EraState.SlotCount) Era.ClockSlots[slot] = i + 1;
            }
            int first = Era.ClockSlots[0] > 0 ? Era.ClockSlots[0] - 1 : 0;
            Era.CurrentSlot = 0;
            Era.CurrentEra = Era.PreviousEra = first;
            Era.StartedYear = 0;
            var def = Content.Eras[first];
            Era.DurationYears = rng.Range(def.MinYears, def.MaxYears + 1);
            Era.TintBlend = 1f;
            Mods = NatureModifiers.From(def);
        }

        // After worldgen or load: re-derive the active lists from the map (burning flags, lava and goo tiles).
        public void RebuildFromMap()
        {
            Burning.Clear();
            Lava.Clear();
            Goo.Clear();
            var t = World.Tables;
            for (int i = 0; i < World.TileCount; i++)
            {
                if ((World.Flags[i] & (ushort)TileFlags.Burning) != 0 && Burning.Length < MaxBurning) Burning.Add(i);
                byte g = World.Ground[i];
                if (t.Active[g] == 0) continue;
                if (t.Spreads[g] != 0) Goo.Add(i);
                if (t.Flows[g] != 0 || t.DecayTicks[g] != 0)
                    Lava.Add(new LavaCell { Tile = i, Type = g, DueTick = t.DecayTicks[g], BaseLevel = TileTables.FallbackReliefLevel });
            }
            World.ActivatedTiles.Clear();
        }

        // --- Tile actions shared by systems and powers ---

        public bool CanBurn(int i)
        {
            var t = World.Tables;
            ushort flags = World.Flags[i];
            if ((flags & (ushort)TileFlags.Water) != 0) return false;
            if (t.BiomeFireproof[World.Biome[i]] != 0) return false;
            return (flags & (ushort)TileFlags.Burnable) != 0 || t.FeatureBurnable[World.Feature[i]] != 0 || World.Building[i] >= 0;
        }

        // force: ignite even without fuel (infernal sparks, lightning on rock); the fire then burns out on its own.
        public bool Ignite(int x, int y, byte intensity, bool force = false)
        {
            int i = World.Index(x, y);
            ushort flags = World.Flags[i];
            if ((flags & (ushort)TileFlags.Water) != 0) return false;
            if ((flags & (ushort)TileFlags.Burning) != 0)
            {
                if (World.Fire[i] < intensity) World.SetFire(x, y, intensity);
                return true;
            }
            if (!force && !CanBurn(i)) return false;
            if (Burning.Length >= MaxBurning) return false;
            World.SetFire(x, y, intensity);
            World.SetFlag(x, y, TileFlags.Burning, true);
            Burning.Add(i);
            return true;
        }

        // Rain / extinguish: fire intensity 0; the fire system removes the flag on its next step without scorching.
        public void Douse(int x, int y)
        {
            int i = World.Index(x, y);
            if ((World.Flags[i] & (ushort)TileFlags.Burning) != 0) World.SetFire(x, y, 0);
        }

        public bool Freeze(int x, int y)
        {
            byte to = World.Tables.FreezeTo[World.Ground[World.Index(x, y)]];
            if (to == TileTables.NoStep) return false;
            World.SetGround(x, y, to, ChangeSource.Nature);
            return true;
        }

        // Lava one step cooler (rain, cool_lava power).
        public bool CoolLava(int x, int y)
        {
            byte g = World.Ground[World.Index(x, y)];
            if (World.Tables.Material[g] != TileTables.MaterialLava) return false;
            byte to = World.Tables.DecayTo[g];
            if (to == TileTables.NoStep) return false;
            World.SetGround(x, y, to, ChangeSource.Nature);
            return true;
        }

        public void RainDrop(int x, int y, ref SimRandom rng)
        {
            int i = World.Index(x, y);
            var t = World.Tables;
            Douse(x, y);
            byte g = World.Ground[i];
            if (t.Material[g] == TileTables.MaterialLava) { CoolLava(x, y); return; }
            if (t.RecoverNeedsWater[g] != 0 && rng.Chance(1f / 20f)) { World.SetGround(x, y, t.RecoverTo[g], ChangeSource.Nature); return; }
            FeatureGrowth.TryGrow(this, x, y, ref rng);
        }

        public void SnowDrop(int x, int y, ref SimRandom rng)
        {
            int i = World.Index(x, y);
            ushort flags = World.Flags[i];
            if ((flags & (ushort)TileFlags.Water) == 0) World.SetFlag(x, y, TileFlags.SnowCover, true);
            else if (rng.Chance(0.2f)) Freeze(x, y);
        }

        public bool AddSeed(int x, int y, byte biome)
        {
            if (Seeds.Length >= MaxSeeds) return false;
            Seeds.Add(new SeedData { Tile = World.Index(x, y), Biome = biome, RemainingYears = 5 });
            return true;
        }

        public bool SpawnCloud(int type, float2 pos, ref SimRandom rng)
        {
            if (Clouds.Length >= MaxClouds || type < 0 || type >= Content.Clouds.Count) return false;
            Clouds.Add(new CloudData
            {
                Pos = pos,
                Radius = rng.Range(6f, 14f),
                Type = (ushort)type,
                LifetimeTicks = rng.Range(600, 2401),
                NextDropTick = 0,
            });
            return true;
        }

        public void Dispose()
        {
            if (Clouds.IsCreated) Clouds.Dispose();
            if (Seeds.IsCreated) Seeds.Dispose();
            if (Burning.IsCreated) Burning.Dispose();
            if (Lava.IsCreated) Lava.Dispose();
            if (Goo.IsCreated) Goo.Dispose();
            if (ZoneAshMonths.IsCreated) ZoneAshMonths.Dispose();
        }
    }

    public readonly struct EraChangedEvent : ISimEvent
    {
        public readonly int OldEra, NewEra;

        public EraChangedEvent(int oldEra, int newEra)
        {
            OldEra = oldEra;
            NewEra = newEra;
        }
    }

    // Sim decides, Powers executes (Bölüm 2.13): DisasterExecutor listens for this.
    public readonly struct DisasterRequestEvent : ISimEvent
    {
        public readonly int Disaster;   // DisasterDef index
        public readonly int X, Y;
        public readonly int Power;      // PowerDef index, -1 = none

        public DisasterRequestEvent(int disaster, int x, int y, int power)
        {
            Disaster = disaster;
            X = x;
            Y = y;
            Power = power;
        }
    }
}
