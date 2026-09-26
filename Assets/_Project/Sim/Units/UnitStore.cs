using System;
using System.IO;
using System.Runtime.InteropServices;
using PG.Content;
using PG.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    public enum DeathCause : byte { OldAge, Killed, Starved, Disease, Burned, Drowned, Frozen, Explosion, Crushed, Divine, Transformed, Void }

    public enum AgeStage : byte { Baby, Child, Adult, Elder }

    // 256 trait bits (unit_traits.json indices).
    public struct TraitSet
    {
        public ulong A, B, C, D;

        public bool Has(int i)
        {
            ulong bit = 1UL << (i & 63);
            switch (i >> 6) { case 0: return (A & bit) != 0; case 1: return (B & bit) != 0; case 2: return (C & bit) != 0; default: return (D & bit) != 0; }
        }

        public void Set(int i, bool on)
        {
            ulong bit = 1UL << (i & 63);
            switch (i >> 6)
            {
                case 0: A = on ? A | bit : A & ~bit; break;
                case 1: B = on ? B | bit : B & ~bit; break;
                case 2: C = on ? C | bit : C & ~bit; break;
                default: D = on ? D | bit : D & ~bit; break;
            }
        }

        public bool IsEmpty => (A | B | C | D) == 0;
    }

    public struct UnitSpawnRequest
    {
        public int Species;
        public float2 Pos;
        public float AgeYears;
        public int Sex;          // -1 = random
        public EntityId Mother, Father;
        public int Subspecies;   // -1 = none (Bölüm 4)
    }

    public struct Corpse
    {
        public int Tile;
        public ushort Species;
        public long Uid;
        public long Tick;
    }

    // Bölüm 3.1: every unit in one SoA store. Slots are reused; EntityId generations catch stale references.
    // Kill() only marks the unit; the slot is freed in the EventsFlush phase so every system of the tick reads consistent data.
    public sealed class UnitStore : IDisposable
    {
        public const int StatCount = (int)StatId.Count;
        public const int StatusSlots = 8, SpellSlots = 4, EquipSlots = 7, HapSlots = 8;
        public const byte StateFree = 0, StateAlive = 1, StateDying = 2;
        public const int InitialCapacity = 1024;

        // identity
        public NativeArray<int> Generation;
        public NativeArray<byte> State;
        public NativeArray<long> Uid;
        public NativeArray<ushort> Species;
        public NativeArray<int> Subspecies;
        public NativeArray<byte> Sex;
        public NativeArray<long> BirthTick;
        // position
        public NativeArray<float2> Pos;
        public NativeArray<float> Z, VZ;
        public NativeArray<byte> Facing;
        // state
        public NativeArray<float> Hp, Mana, Stamina;
        public NativeArray<byte> Saturation, Energy, StarveMonths, Age;
        public NativeArray<sbyte> Happiness;
        public NativeArray<int> Xp;
        public NativeArray<byte> Level;
        public NativeArray<ushort> Kills;
        // derived
        public NativeArray<float> Stats;        // capacity * StatCount
        public NativeArray<byte> StatsDirty;
        public NativeArray<TraitSet> Traits;
        public NativeArray<ushort> StatusId;    // capacity * StatusSlots, 0 = empty, else StatusEffectDef index + 1
        public NativeArray<int> StatusLeft;     // ticks, -1 = until removed
        public NativeArray<uint> BaseFlags;     // species + traits
        public NativeArray<uint> Flags;         // base + statuses
        // AI
        public NativeArray<byte> Task, Action;
        public NativeArray<short> Timer;
        public NativeArray<EntityId> Target;
        public NativeArray<int2> TargetTile;
        public NativeArray<int> PathHandle;
        public NativeArray<ushort> PathStep;
        public NativeArray<byte> PathFails;
        public NativeArray<long> NextThink;
        public NativeArray<EntityId> LastAttacker;
        public NativeArray<long> LastAttackedTick;
        public NativeArray<short> AttackCooldown;
        public NativeArray<ushort> SpellCooldown; // capacity * SpellSlots
        // social
        public NativeArray<EntityId> Mother, Father, Mate;
        public NativeArray<int2> Home;
        public NativeArray<byte> Wants;         // bit 0: wants to mate (monthly fertility roll)
        // civilization (Bölüm 5) — saved by the civ section (CivSave), not section 10
        public NativeArray<int> City;           // -1 = none
        public NativeArray<byte> Job;           // JobDef index + 1, 0 = none
        public NativeArray<int> HomeBuilding, WorkBuilding; // -1 = none
        public NativeArray<short> CarryRes;     // resource index, -1 = empty hands
        public NativeArray<byte> CarryAmount;
        public NativeArray<byte> WorkOp;        // current step of the job (CivWork)
        public NativeArray<int> Equip;          // capacity * EquipSlots, item index, -1 = empty
        public NativeArray<ushort> HapEvent;    // capacity * HapSlots, HappinessEventDef index + 1, 0 = empty
        public NativeArray<byte> HapLeft;       // months left per happiness slot
        public IItemSource Items;               // equipment effects (set by CivState)

        public NativeList<int> Alive;
        public NativeList<Corpse> Corpses;
        NativeList<int> _free;
        NativeList<int3> _dying; // (index, cause, killer index)

        public long NextUid = 1;
        readonly ContentDB _content;

        public UnitStore(ContentDB content, int capacity = InitialCapacity)
        {
            _content = content;
            Capacity = 0;
            Alive = new NativeList<int>(capacity, Allocator.Persistent);
            Corpses = new NativeList<Corpse>(64, Allocator.Persistent);
            _free = new NativeList<int>(64, Allocator.Persistent);
            _dying = new NativeList<int3>(16, Allocator.Persistent);
            Resize(capacity);
        }

        public int Capacity { get; private set; }
        public int HighWater { get; private set; } // slots ever used
        public int Count => Alive.Length;
        public ContentDB Content => _content;

        public bool IsAlive(EntityId id) => id.Index >= 0 && id.Index < HighWater && Generation[id.Index] == id.Generation && State[id.Index] == StateAlive;
        public bool IsAlive(int index) => index >= 0 && index < HighWater && State[index] == StateAlive;
        public EntityId IdOf(int index) => new EntityId(index, Generation[index]);
        public int2 Tile(int index) => (int2)math.floor(Pos[index]);
        public SpeciesDef SpeciesOf(int index) => _content.Species[Species[index]];
        public bool Has(int index, UnitFlags flag) => (Flags[index] & (uint)flag) != 0;
        public float AgeYears(int index, long tick) => (tick - BirthTick[index]) / (float)SimConst.TicksPerYear;

        public float Stat(int index, StatId stat)
        {
            if (StatsDirty[index] != 0) UnitStats.Recompute(this, index);
            return Stats[index * StatCount + (int)stat];
        }

        public EntityId Spawn(in UnitSpawnRequest req, long tick, ref SimRandom rng)
        {
            int i;
            if (_free.Length > 0)
            {
                i = _free[_free.Length - 1];
                _free.RemoveAt(_free.Length - 1);
            }
            else
            {
                if (HighWater == Capacity) Resize(Capacity * 2);
                i = HighWater++;
            }

            var sp = _content.Species[req.Species];
            State[i] = StateAlive;
            Uid[i] = NextUid++;
            Species[i] = (ushort)req.Species;
            Subspecies[i] = req.Subspecies;
            Sex[i] = (byte)(req.Sex >= 0 ? req.Sex : sp.Reproduction == Reproduction.Live || sp.Reproduction == Reproduction.Egg ? rng.Range(0, 2) : 2);
            BirthTick[i] = tick - (long)(req.AgeYears * SimConst.TicksPerYear);
            Pos[i] = req.Pos;
            Z[i] = 0f;
            VZ[i] = 0f;
            Facing[i] = (byte)rng.Range(0, 2);
            Saturation[i] = 80;
            Energy[i] = 80;
            StarveMonths[i] = 0;
            Happiness[i] = 0;
            Xp[i] = 0;
            Level[i] = 1;
            Kills[i] = 0;

            var traits = default(TraitSet);
            foreach (int t in sp.GrantTraits) traits.Set(t, true);
            Traits[i] = traits;
            for (int s = 0; s < StatusSlots; s++)
            {
                StatusId[i * StatusSlots + s] = 0;
                StatusLeft[i * StatusSlots + s] = 0;
            }
            for (int s = 0; s < SpellSlots; s++) SpellCooldown[i * SpellSlots + s] = 0;

            Task[i] = 0;
            Action[i] = 0;
            Timer[i] = 0;
            Target[i] = EntityId.None;
            TargetTile[i] = (int2)math.floor(req.Pos);
            PathHandle[i] = -1;
            PathStep[i] = 0;
            PathFails[i] = 0;
            NextThink[i] = tick + rng.Range(0, 10);
            LastAttacker[i] = EntityId.None;
            LastAttackedTick[i] = long.MinValue / 2;
            AttackCooldown[i] = 0;
            Mother[i] = req.Mother;
            Father[i] = req.Father;
            Mate[i] = EntityId.None;
            Home[i] = new int2(-1, -1);
            Wants[i] = 0;
            City[i] = -1;
            Job[i] = 0;
            HomeBuilding[i] = -1;
            WorkBuilding[i] = -1;
            CarryRes[i] = -1;
            CarryAmount[i] = 0;
            WorkOp[i] = 0;
            for (int s = 0; s < EquipSlots; s++) Equip[i * EquipSlots + s] = -1;
            for (int s = 0; s < HapSlots; s++)
            {
                HapEvent[i * HapSlots + s] = 0;
                HapLeft[i * HapSlots + s] = 0;
            }

            Age[i] = (byte)UnitStats.StageFor(sp, AgeYears(i, tick));
            StatsDirty[i] = 1;
            UnitStats.Recompute(this, i);
            Hp[i] = Stat(i, StatId.Hp);
            Mana[i] = Stat(i, StatId.Mana);
            Stamina[i] = Stat(i, StatId.Stamina);

            Alive.Add(i);
            return new EntityId(i, Generation[i]);
        }

        public void Kill(int index, DeathCause cause, int killer = -1)
        {
            if (!IsAlive(index)) return;
            State[index] = StateDying;
            _dying.Add(new int3(index, (int)cause, killer));
        }

        // EventsFlush phase: frees dying slots, leaves corpses, publishes UnitDiedEvent.
        public void FlushDeaths(EventBus events, long tick, int mapWidth)
        {
            if (_dying.Length == 0) return;
            for (int k = 0; k < _dying.Length; k++)
            {
                int i = _dying[k].x;
                var cause = (DeathCause)_dying[k].y;
                int killer = _dying[k].z;
                int2 tile = Tile(i);
                long killerUid = killer >= 0 && killer < HighWater ? Uid[killer] : 0;
                events?.Publish(new UnitDiedEvent(Uid[i], Species[i], cause, killerUid, tile));

                var sp = _content.Species[Species[i]];
                bool leavesBody = cause != DeathCause.Void && cause != DeathCause.Transformed && cause != DeathCause.Explosion && sp.Category != "elemental";
                if (leavesBody && Corpses.Length < 4096)
                    Corpses.Add(new Corpse { Tile = tile.y * mapWidth + tile.x, Species = Species[i], Uid = Uid[i], Tick = tick });

                State[i] = StateFree;
                Generation[i]++;
                _free.Add(i);
                int at = Alive.AsArray().IndexOf(i);
                if (at >= 0) Alive.RemoveAtSwapBack(at);
            }
            _dying.Clear();
        }

        // --- statuses ---

        public bool HasStatus(int index, int status)
        {
            ushort id = (ushort)(status + 1);
            for (int s = 0; s < StatusSlots; s++)
                if (StatusId[index * StatusSlots + s] == id) return true;
            return false;
        }

        // Same status refreshes to the longer duration; otherwise a free slot, otherwise the slot closest to expiring.
        public void AddStatus(int index, int status, int ticks)
        {
            if (status < 0) return;
            ushort id = (ushort)(status + 1);
            int baseSlot = index * StatusSlots, free = -1, shortest = -1, shortestLeft = int.MaxValue;
            for (int s = 0; s < StatusSlots; s++)
            {
                int k = baseSlot + s;
                if (StatusId[k] == id)
                {
                    if (ticks < 0 || StatusLeft[k] < 0) StatusLeft[k] = -1;
                    else StatusLeft[k] = math.max(StatusLeft[k], ticks);
                    return;
                }
                if (StatusId[k] == 0 && free < 0) free = k;
                int left = StatusLeft[k] < 0 ? int.MaxValue - 1 : StatusLeft[k];
                if (StatusId[k] != 0 && left < shortestLeft) { shortestLeft = left; shortest = k; }
            }
            int slot = free >= 0 ? free : shortest;
            if (slot < 0) return;
            StatusId[slot] = id;
            StatusLeft[slot] = ticks;
            StatsDirty[index] = 1;
        }

        public void RemoveStatus(int index, int status)
        {
            ushort id = (ushort)(status + 1);
            for (int s = 0; s < StatusSlots; s++)
            {
                int k = index * StatusSlots + s;
                if (StatusId[k] != id) continue;
                StatusId[k] = 0;
                StatusLeft[k] = 0;
                StatsDirty[index] = 1;
            }
        }

        // --- storage ---

        void Resize(int capacity)
        {
            Grow(ref Generation, capacity);
            Grow(ref State, capacity);
            Grow(ref Uid, capacity);
            Grow(ref Species, capacity);
            Grow(ref Subspecies, capacity);
            Grow(ref Sex, capacity);
            Grow(ref BirthTick, capacity);
            Grow(ref Pos, capacity);
            Grow(ref Z, capacity);
            Grow(ref VZ, capacity);
            Grow(ref Facing, capacity);
            Grow(ref Hp, capacity);
            Grow(ref Mana, capacity);
            Grow(ref Stamina, capacity);
            Grow(ref Saturation, capacity);
            Grow(ref Energy, capacity);
            Grow(ref StarveMonths, capacity);
            Grow(ref Age, capacity);
            Grow(ref Happiness, capacity);
            Grow(ref Xp, capacity);
            Grow(ref Level, capacity);
            Grow(ref Kills, capacity);
            Grow(ref Stats, capacity * StatCount);
            Grow(ref StatsDirty, capacity);
            Grow(ref Traits, capacity);
            Grow(ref StatusId, capacity * StatusSlots);
            Grow(ref StatusLeft, capacity * StatusSlots);
            Grow(ref BaseFlags, capacity);
            Grow(ref Flags, capacity);
            Grow(ref Task, capacity);
            Grow(ref Action, capacity);
            Grow(ref Timer, capacity);
            Grow(ref Target, capacity);
            Grow(ref TargetTile, capacity);
            Grow(ref PathHandle, capacity);
            Grow(ref PathStep, capacity);
            Grow(ref PathFails, capacity);
            Grow(ref NextThink, capacity);
            Grow(ref LastAttacker, capacity);
            Grow(ref LastAttackedTick, capacity);
            Grow(ref AttackCooldown, capacity);
            Grow(ref SpellCooldown, capacity * SpellSlots);
            Grow(ref Mother, capacity);
            Grow(ref Father, capacity);
            Grow(ref Mate, capacity);
            Grow(ref Home, capacity);
            Grow(ref Wants, capacity);
            ForEachCivArray(new Grower(capacity));
            Capacity = capacity;
        }

        static void Grow<T>(ref NativeArray<T> array, int length) where T : struct
        {
            var next = new NativeArray<T>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (array.IsCreated)
            {
                NativeArray<T>.Copy(array, next, math.min(array.Length, length));
                array.Dispose();
            }
            array = next;
        }

        // --- save / load (Persistence section 10): raw slot arrays up to HighWater + lists, so a loaded world continues identically ---

        public void Write(BinaryWriter w)
        {
            w.Write(HighWater);
            w.Write(NextUid);
            ForEachArray(new Writer(w, HighWater));
            WriteList(w, Alive);
            WriteList(w, _free);
            WriteList(w, Corpses);
        }

        public void Read(BinaryReader r, ushort[] speciesRemap, int[] traitRemap, int[] statusRemap)
        {
            int high = r.ReadInt32();
            NextUid = r.ReadInt64();
            int cap = InitialCapacity;
            while (cap < high) cap *= 2;
            if (cap > Capacity) Resize(cap);
            HighWater = high;
            ForEachArray(new Reader(r, high));
            ReadList(r, Alive);
            ReadList(r, _free);
            ReadList(r, Corpses);

            for (int i = 0; i < high; i++)
            {
                if (State[i] == StateFree) continue;
                Species[i] = Species[i] < speciesRemap.Length ? speciesRemap[Species[i]] : (ushort)0;
                var old = Traits[i];
                var mapped = default(TraitSet);
                for (int t = 0; t < traitRemap.Length; t++)
                    if (traitRemap[t] >= 0 && old.Has(t)) mapped.Set(traitRemap[t], true);
                Traits[i] = mapped;
                for (int s = 0; s < StatusSlots; s++)
                {
                    int k = i * StatusSlots + s;
                    int id = StatusId[k] - 1;
                    int to = id >= 0 && id < statusRemap.Length ? statusRemap[id] : -1;
                    StatusId[k] = (ushort)(to + 1);
                }
                StatsDirty[i] = 1;
            }
            for (int k = 0; k < Corpses.Length; k++)
            {
                var c = Corpses[k];
                c.Species = c.Species < speciesRemap.Length ? speciesRemap[c.Species] : (ushort)0;
                Corpses[k] = c;
            }
            for (int i = 0; i < high; i++)
                if (State[i] != StateFree) UnitStats.Recompute(this, i); // flags and stats are derived, not saved
        }

        interface IArrayVisitor
        {
            void Visit<T>(ref NativeArray<T> array, int stride) where T : struct;
        }

        void ForEachArray<TV>(TV v) where TV : IArrayVisitor
        {
            v.Visit(ref Generation, 1); v.Visit(ref State, 1); v.Visit(ref Uid, 1); v.Visit(ref Species, 1); v.Visit(ref Subspecies, 1);
            v.Visit(ref Sex, 1); v.Visit(ref BirthTick, 1); v.Visit(ref Pos, 1); v.Visit(ref Z, 1); v.Visit(ref VZ, 1); v.Visit(ref Facing, 1);
            v.Visit(ref Hp, 1); v.Visit(ref Mana, 1); v.Visit(ref Stamina, 1); v.Visit(ref Saturation, 1); v.Visit(ref Energy, 1);
            v.Visit(ref StarveMonths, 1); v.Visit(ref Age, 1); v.Visit(ref Happiness, 1); v.Visit(ref Xp, 1); v.Visit(ref Level, 1);
            v.Visit(ref Kills, 1); v.Visit(ref Traits, 1); v.Visit(ref StatusId, StatusSlots); v.Visit(ref StatusLeft, StatusSlots);
            v.Visit(ref Task, 1); v.Visit(ref Action, 1); v.Visit(ref Timer, 1); v.Visit(ref Target, 1); v.Visit(ref TargetTile, 1);
            v.Visit(ref PathHandle, 1); v.Visit(ref PathStep, 1); v.Visit(ref PathFails, 1); v.Visit(ref NextThink, 1);
            v.Visit(ref LastAttacker, 1); v.Visit(ref LastAttackedTick, 1); v.Visit(ref AttackCooldown, 1);
            v.Visit(ref SpellCooldown, SpellSlots); v.Visit(ref Mother, 1); v.Visit(ref Father, 1); v.Visit(ref Mate, 1); v.Visit(ref Home, 1); v.Visit(ref Wants, 1);
        }

        // Civ arrays (Bölüm 5) live in their own save section so section 10 keeps its layout.
        void ForEachCivArray<TV>(TV v) where TV : IArrayVisitor
        {
            v.Visit(ref City, 1); v.Visit(ref Job, 1); v.Visit(ref HomeBuilding, 1); v.Visit(ref WorkBuilding, 1);
            v.Visit(ref CarryRes, 1); v.Visit(ref CarryAmount, 1); v.Visit(ref WorkOp, 1); v.Visit(ref Equip, EquipSlots);
            v.Visit(ref HapEvent, HapSlots); v.Visit(ref HapLeft, HapSlots);
        }

        public void WriteCiv(BinaryWriter w) => ForEachCivArray(new Writer(w, HighWater));

        public void ReadCiv(BinaryReader r) => ForEachCivArray(new Reader(r, HighWater));

        // Defaults for a save without the civ section: nobody belongs anywhere.
        public void ResetCiv()
        {
            for (int i = 0; i < HighWater; i++)
            {
                City[i] = -1; Job[i] = 0; HomeBuilding[i] = -1; WorkBuilding[i] = -1; CarryRes[i] = -1; CarryAmount[i] = 0; WorkOp[i] = 0;
                for (int s = 0; s < EquipSlots; s++) Equip[i * EquipSlots + s] = -1;
                for (int s = 0; s < HapSlots; s++) { HapEvent[i * HapSlots + s] = 0; HapLeft[i * HapSlots + s] = 0; }
            }
        }

        readonly struct Grower : IArrayVisitor
        {
            readonly int _capacity;
            public Grower(int capacity) { _capacity = capacity; }
            public void Visit<T>(ref NativeArray<T> array, int stride) where T : struct => Grow(ref array, _capacity * stride);
        }

        readonly struct Writer : IArrayVisitor
        {
            readonly BinaryWriter _w;
            readonly int _count;
            public Writer(BinaryWriter w, int count) { _w = w; _count = count; }

            public void Visit<T>(ref NativeArray<T> array, int stride) where T : struct =>
                _w.Write(MemoryMarshal.AsBytes(array.AsReadOnlySpan().Slice(0, _count * stride)));
        }

        readonly struct Reader : IArrayVisitor
        {
            readonly BinaryReader _r;
            readonly int _count;
            public Reader(BinaryReader r, int count) { _r = r; _count = count; }

            public void Visit<T>(ref NativeArray<T> array, int stride) where T : struct
            {
                var bytes = MemoryMarshal.AsBytes(array.AsSpan().Slice(0, _count * stride));
                int read = _r.Read(bytes);
                if (read != bytes.Length) throw new EndOfStreamException("Truncated unit data");
            }
        }

        static void WriteList<T>(BinaryWriter w, NativeList<T> list) where T : unmanaged
        {
            w.Write(list.Length);
            w.Write(MemoryMarshal.AsBytes(list.AsArray().AsReadOnlySpan()));
        }

        static void ReadList<T>(BinaryReader r, NativeList<T> list) where T : unmanaged
        {
            int n = r.ReadInt32();
            list.Resize(n, NativeArrayOptions.ClearMemory);
            var bytes = MemoryMarshal.AsBytes(list.AsArray().AsSpan());
            if (r.Read(bytes) != bytes.Length) throw new EndOfStreamException("Truncated unit list");
        }

        public void Dispose()
        {
            ForEachArray(new Disposer());
            ForEachCivArray(new Disposer());
            if (Stats.IsCreated) Stats.Dispose();
            if (StatsDirty.IsCreated) StatsDirty.Dispose();
            if (BaseFlags.IsCreated) BaseFlags.Dispose();
            if (Flags.IsCreated) Flags.Dispose();
            if (Alive.IsCreated) Alive.Dispose();
            if (Corpses.IsCreated) Corpses.Dispose();
            if (_free.IsCreated) _free.Dispose();
            if (_dying.IsCreated) _dying.Dispose();
        }

        readonly struct Disposer : IArrayVisitor
        {
            public void Visit<T>(ref NativeArray<T> array, int stride) where T : struct
            {
                if (array.IsCreated) array.Dispose();
            }
        }
    }

    public readonly struct UnitDiedEvent : ISimEvent
    {
        public readonly long Uid;
        public readonly ushort Species;
        public readonly DeathCause Cause;
        public readonly long KillerUid;
        public readonly int2 Tile;

        public UnitDiedEvent(long uid, ushort species, DeathCause cause, long killerUid, int2 tile)
        {
            Uid = uid;
            Species = species;
            Cause = cause;
            KillerUid = killerUid;
            Tile = tile;
        }
    }

    public readonly struct UnitBornEvent : ISimEvent
    {
        public readonly long Uid, MotherUid, FatherUid;
        public readonly ushort Species;

        public UnitBornEvent(long uid, ushort species, long motherUid, long fatherUid)
        {
            Uid = uid;
            Species = species;
            MotherUid = motherUid;
            FatherUid = fatherUid;
        }
    }

    public readonly struct UnitLevelUpEvent : ISimEvent
    {
        public readonly long Uid;
        public readonly byte Level;

        public UnitLevelUpEvent(long uid, byte level)
        {
            Uid = uid;
            Level = level;
        }
    }
}
