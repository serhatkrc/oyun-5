using System;
using System.Collections.Generic;
using PG.Content;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    public enum PathStatus : byte { None, Ready, Unreachable, Failed, OverBudget }

    // Which tiles a unit may stand on.
    public enum Mobility : byte { Land, Water, Amphibious, Fly }

    // Bölüm 3.4: island check -> region A* (corridor) -> tile A* inside the corridor, 8 directions, no corner cutting.
    // Paths live as slices of one shared list; released slices are compacted away when garbage piles up.
    // ponytail: solved synchronously on the main thread within a per-tick budget; no corridor cache yet (region A* is cheap).
    public sealed class PathService : IDisposable
    {
        public const int MaxPathLength = 512;
        public const int RequestsPerTick = 64;
        public const int MaxTileExpansions = 12000;
        public const int NearbyFreeRadius = 3;
        public const int StraightMaxRange = 32;   // DECISIONS #51: clear straight lines skip A* (no budget)
        public const float MaxDetour = 3f, DetourSlack = 64f; // region route longer than this -> Unreachable (DECISIONS #51)

        readonly WorldMap _map;
        readonly RegionGraph _regions;

        NativeList<int2> _points;
        readonly List<int> _start = new List<int>(), _length = new List<int>();
        readonly Stack<int> _freeHandles = new Stack<int>();
        int _garbage;

        // search scratch
        int _stamp, _corridorValue;
        int[] _tileStamp, _tileParent, _closedStamp;
        float[] _tileG;
        int[] _regionStamp, _regionParent, _corridorStamp;
        float[] _regionG;
        readonly MinHeap _heap = new MinHeap(4096);
        readonly List<int2> _scratch = new List<int2>(MaxPathLength);

        static readonly int2[] Dirs8 =
        {
            new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1),
            new int2(1, 1), new int2(1, -1), new int2(-1, 1), new int2(-1, -1),
        };

        public PathService(WorldMap map, RegionGraph regions)
        {
            _map = map;
            _regions = regions;
            _points = new NativeList<int2>(4096, Allocator.Persistent);
            int n = map.TileCount;
            _tileStamp = new int[n];
            _tileParent = new int[n];
            _closedStamp = new int[n];
            _tileG = new float[n];
        }

        public int SolvedThisTick { get; private set; }
        // diagnostics only (not simulation state)
        public long StatSearches, StatExpansions, StatFailed, StatStraight, StatDetour;
        public int Budget { get; set; } = RequestsPerTick;

        public void BeginTick() => SolvedThisTick = 0;

        public int Length(int handle) => handle >= 0 && handle < _length.Count ? _length[handle] : 0;
        public int2 Point(int handle, int step) => _points[_start[handle] + step];

        public void Release(int handle)
        {
            if (handle < 0 || handle >= _length.Count || _length[handle] < 0) return;
            _garbage += _length[handle];
            _length[handle] = -1;
            _freeHandles.Push(handle);
            if (_garbage > 4096 && _garbage > _points.Length / 2) Compact();
        }

        public bool SameIsland(int2 a, int2 b, Mobility m)
        {
            if (m == Mobility.Fly) return true;
            var cls = m == Mobility.Water ? MoveClass.Water : MoveClass.Land;
            return _regions.SameIsland(a.x, a.y, b.x, b.y, cls);
        }

        public static Mobility MobilityOf(UnitStore u, int i)
        {
            uint f = u.Flags[i];
            if ((f & (uint)UnitFlags.Fly) != 0) return Mobility.Fly;
            if ((f & (uint)UnitFlags.BreatheWater) != 0 && u.SpeciesOf(i).Habitat == Habitat.Water) return Mobility.Water;
            if ((f & (uint)UnitFlags.Swim) != 0) return Mobility.Amphibious;
            return Mobility.Land;
        }

        public bool CanStand(int x, int y, Mobility m)
        {
            if (!_map.InBounds(x, y)) return false;
            ushort f = _map.Flags[_map.Index(x, y)];
            switch (m)
            {
                case Mobility.Fly: return true;
                case Mobility.Water: return (f & (ushort)TileFlags.Water) != 0;
                case Mobility.Amphibious: return (f & (ushort)(TileFlags.Walkable | TileFlags.Water)) != 0;
                default: return (f & (ushort)TileFlags.Walkable) != 0;
            }
        }

        // Nearest standable tile around `to` (targets on buildings, water, mountains).
        public bool NearestStandable(int2 to, Mobility m, out int2 result)
        {
            result = to;
            if (CanStand(to.x, to.y, m)) return true;
            for (int r = 1; r <= NearbyFreeRadius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        if (!CanStand(to.x + dx, to.y + dy, m)) continue;
                        result = to + new int2(dx, dy);
                        return true;
                    }
            return false;
        }

        public PathStatus Request(int2 from, int2 to, Mobility m, out int handle, bool countBudget = true)
        {
            handle = -1;
            if (!_map.InBounds(from.x, from.y) || !_map.InBounds(to.x, to.y)) return PathStatus.Failed;
            if (!NearestStandable(to, m, out to)) return PathStatus.Unreachable;

            if (m == Mobility.Fly || math.all(from == to))
            {
                _scratch.Clear();
                _scratch.Add(to);
                handle = Store(_scratch);
                return PathStatus.Ready;
            }

            // Layer 1: islands (instant, no search).
            if (m == Mobility.Land || m == Mobility.Water)
            {
                var cls = m == Mobility.Land ? MoveClass.Land : MoveClass.Water;
                if (_regions.RegionAt(from.x, from.y, cls) < 0) return PathStatus.Failed; // standing somewhere odd (terraformed under it)
                if (!_regions.SameIsland(from.x, from.y, to.x, to.y, cls)) return PathStatus.Unreachable;
            }

            if (StraightLine(from, to, m))
            {
                StatStraight++;
                handle = Store(_scratch);
                return PathStatus.Ready;
            }

            if (countBudget)
            {
                if (SolvedThisTick >= Budget) return PathStatus.OverBudget;
                SolvedThisTick++;
            }

            bool corridor = false;
            if (m == Mobility.Land || m == Mobility.Water)
            {
                var cls = m == Mobility.Land ? MoveClass.Land : MoveClass.Water;
                int startRegion = _regions.RegionAt(from.x, from.y, cls), goalRegion = _regions.RegionAt(to.x, to.y, cls);
                corridor = RegionCorridor(startRegion, goalRegion, out float route);
                if (!corridor) return PathStatus.Unreachable;
                // a short hop that needs a long way round (across an inlet or lake) is not worth a tile search;
                // the route runs between region centres, so it is compared with the centres' own distance
                float direct = Octile(_regions.GetRegion(startRegion).Center, _regions.GetRegion(goalRegion).Center);
                if (route > direct * MaxDetour + DetourSlack) { StatDetour++; return PathStatus.Unreachable; }
            }

            StatSearches++;
            if (!TileAStar(from, to, m, corridor)) { StatFailed++; return PathStatus.Failed; }
            handle = Store(_scratch);
            return PathStatus.Ready;
        }

        // Layer 2: A* over the region graph; marks the regions on the found route (and their neighbours, for slack).
        bool RegionCorridor(int start, int goal, out float route)
        {
            route = 0f;
            int cap = _regions.RegionCapacity;
            if (_regionStamp == null || _regionStamp.Length < cap)
            {
                int size = math.max(64, math.ceilpow2(cap));
                _regionStamp = new int[size];
                _regionParent = new int[size];
                _corridorStamp = new int[size];
                _regionG = new float[size];
            }
            _stamp++;
            _heap.Clear();
            _regionStamp[start] = _stamp;
            _regionG[start] = 0f;
            _regionParent[start] = -1;
            var goalCenter = (float2)_regions.GetRegion(goal).Center;
            _heap.Push(start, math.distance(_regions.GetRegion(start).Center, goalCenter));

            bool found = false;
            while (_heap.Count > 0)
            {
                int r = _heap.Pop();
                if (r == goal) { found = true; break; }
                var center = (float2)_regions.GetRegion(r).Center;
                var edges = _regions.GetEdges(r);
                for (int e = 0; e < edges.Count; e++)
                {
                    int n = edges[e];
                    if (!_regions.IsAlive(n)) continue;
                    var nc = (float2)_regions.GetRegion(n).Center;
                    float g = _regionG[r] + math.distance(center, nc);
                    if (_regionStamp[n] == _stamp && g >= _regionG[n]) continue;
                    _regionStamp[n] = _stamp;
                    _regionG[n] = g;
                    _regionParent[n] = r;
                    _heap.Push(n, g + math.distance(nc, goalCenter));
                }
            }
            if (!found) return false;
            route = _regionG[goal];

            _corridorValue = _stamp;
            for (int r = goal; r >= 0; r = _regionParent[r])
            {
                _corridorStamp[r] = _stamp;
                var edges = _regions.GetEdges(r);
                for (int e = 0; e < edges.Count; e++) _corridorStamp[edges[e]] = _stamp;
            }
            return true;
        }

        // Layer 3: tile A* (octile heuristic), limited to the corridor when one is given.
        bool TileAStar(int2 from, int2 to, Mobility m, bool corridor)
        {
            int w = _map.Width;
            int start = from.y * w + from.x, goal = to.y * w + to.x;
            var cls = m == Mobility.Water ? MoveClass.Water : MoveClass.Land;
            var layer = cls == MoveClass.Land ? _regions.LandRegion : _regions.WaterRegion;
            var moveCost = _map.Tables.MoveCost;
            int searchStamp = ++_stamp;
            _heap.Clear();
            _tileStamp[start] = searchStamp;
            _tileG[start] = 0f;
            _tileParent[start] = -1;
            _heap.Push(start, Octile(from, to));

            // budget grows with the distance: short hops that need a long detour fail cheaply (DECISIONS #51)
            float direct = Octile(from, to);
            int limit = math.min(MaxTileExpansions, 256 + (int)(direct * direct * 3f + direct * 40f));
            int expansions = 0;
            bool found = false;
            while (_heap.Count > 0)
            {
                int cur = _heap.Pop();
                if (_closedStamp[cur] == searchStamp) continue;
                _closedStamp[cur] = searchStamp;
                if (cur == goal) { found = true; break; }
                if (++expansions > limit) break;
                StatExpansions++;
                int cx = cur % w, cy = cur / w;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Dirs8[d].x, ny = cy + Dirs8[d].y;
                    if (!CanStand(nx, ny, m)) continue;
                    if (d >= 4 && (!CanStand(cx + Dirs8[d].x, cy, m) || !CanStand(cx, cy + Dirs8[d].y, m))) continue; // no corner cutting
                    int n = ny * w + nx;
                    if (_closedStamp[n] == searchStamp) continue;
                    if (corridor)
                    {
                        int region = layer[n];
                        if (region < 0 || region >= _corridorStamp.Length || _corridorStamp[region] != _corridorValue) continue;
                    }
                    float cost = m == Mobility.Land ? math.max(0.1f, moveCost[_map.Ground[n]]) : 1f;
                    float g = _tileG[cur] + (d >= 4 ? 1.41421f : 1f) * cost;
                    if (_tileStamp[n] == searchStamp && g >= _tileG[n]) continue;
                    _tileStamp[n] = searchStamp;
                    _tileG[n] = g;
                    _tileParent[n] = cur;
                    _heap.Push(n, g + Octile(new int2(nx, ny), to));
                }
            }
            if (!found) return false;

            _scratch.Clear();
            for (int t = goal; t >= 0 && t != start; t = _tileParent[t]) _scratch.Add(new int2(t % w, t / w));
            _scratch.Reverse();
            if (_scratch.Count > MaxPathLength) _scratch.RemoveRange(MaxPathLength, _scratch.Count - MaxPathLength); // re-requested at the end
            return true;
        }

        // Straight walk (8-dir steps, no corner cutting) when every tile on the way is standable.
        bool StraightLine(int2 from, int2 to, Mobility m)
        {
            int2 d = to - from;
            if (math.max(math.abs(d.x), math.abs(d.y)) > StraightMaxRange) return false;
            _scratch.Clear();
            int2 p = from, step = (int2)math.sign(d);
            int adx = math.abs(d.x), ady = math.abs(d.y), err = adx - ady;
            while (!math.all(p == to))
            {
                int e2 = 2 * err;
                int2 next = p;
                if (e2 > -ady) { err -= ady; next.x += step.x; }
                if (e2 < adx) { err += adx; next.y += step.y; }
                if (!CanStand(next.x, next.y, m)) return false;
                if (next.x != p.x && next.y != p.y && (!CanStand(next.x, p.y, m) || !CanStand(p.x, next.y, m))) return false;
                _scratch.Add(next);
                p = next;
            }
            return true;
        }

        static float Octile(int2 a, int2 b)
        {
            int dx = math.abs(a.x - b.x), dy = math.abs(a.y - b.y);
            return math.max(dx, dy) + 0.41421f * math.min(dx, dy);
        }

        int Store(List<int2> path)
        {
            int handle;
            if (_freeHandles.Count > 0)
            {
                handle = _freeHandles.Pop();
                _start[handle] = _points.Length;
                _length[handle] = path.Count;
            }
            else
            {
                handle = _start.Count;
                _start.Add(_points.Length);
                _length.Add(path.Count);
            }
            for (int k = 0; k < path.Count; k++) _points.Add(path[k]);
            return handle;
        }

        void Compact()
        {
            var next = new NativeList<int2>(math.max(4096, _points.Length - _garbage), Allocator.Persistent);
            for (int h = 0; h < _start.Count; h++)
            {
                if (_length[h] < 0) continue;
                int s = _start[h];
                _start[h] = next.Length;
                for (int k = 0; k < _length[h]; k++) next.Add(_points[s + k]);
            }
            _points.Dispose();
            _points = next;
            _garbage = 0;
        }

        // --- save / load: handles and slices exactly as they are (units keep their handles) ---
        public void Write(System.IO.BinaryWriter w)
        {
            w.Write(_start.Count);
            for (int h = 0; h < _start.Count; h++)
            {
                w.Write(_length[h]);
                if (_length[h] < 0) continue;
                for (int k = 0; k < _length[h]; k++)
                {
                    var p = _points[_start[h] + k];
                    w.Write(p.x);
                    w.Write(p.y);
                }
            }
            var free = _freeHandles.ToArray(); // top first
            w.Write(free.Length);
            for (int k = free.Length - 1; k >= 0; k--) w.Write(free[k]);
        }

        public void Read(System.IO.BinaryReader r)
        {
            _points.Clear();
            _start.Clear();
            _length.Clear();
            _freeHandles.Clear();
            _garbage = 0;
            int count = r.ReadInt32();
            for (int h = 0; h < count; h++)
            {
                int len = r.ReadInt32();
                _start.Add(_points.Length);
                _length.Add(len);
                for (int k = 0; k < len; k++) _points.Add(new int2(r.ReadInt32(), r.ReadInt32()));
            }
            int free = r.ReadInt32();
            for (int k = 0; k < free; k++) _freeHandles.Push(r.ReadInt32());
        }

        public void Dispose()
        {
            if (_points.IsCreated) _points.Dispose();
        }

        // Binary min-heap of (id, priority); ties broken by id for determinism.
        sealed class MinHeap
        {
            int[] _ids;
            float[] _pri;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                _ids = new int[capacity];
                _pri = new float[capacity];
            }

            public void Clear() => Count = 0;

            public void Push(int id, float priority)
            {
                if (Count == _ids.Length)
                {
                    Array.Resize(ref _ids, Count * 2);
                    Array.Resize(ref _pri, Count * 2);
                }
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (!Less(priority, id, _pri[p], _ids[p])) break;
                    _ids[i] = _ids[p];
                    _pri[i] = _pri[p];
                    i = p;
                }
                _ids[i] = id;
                _pri[i] = priority;
            }

            public int Pop()
            {
                int top = _ids[0];
                int lastId = _ids[--Count];
                float lastPri = _pri[Count];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= Count) break;
                    int c = l + 1 < Count && Less(_pri[l + 1], _ids[l + 1], _pri[l], _ids[l]) ? l + 1 : l;
                    if (!Less(_pri[c], _ids[c], lastPri, lastId)) break;
                    _ids[i] = _ids[c];
                    _pri[i] = _pri[c];
                    i = c;
                }
                _ids[i] = lastId;
                _pri[i] = lastPri;
                return top;
            }

            static bool Less(float pa, int ia, float pb, int ib) => pa < pb || (pa == pb && ia < ib);
        }
    }
}
