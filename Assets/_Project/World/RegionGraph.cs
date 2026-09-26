using System;
using System.Collections.Generic;
using PG.Content;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.World
{
    public enum MoveClass : byte { Land, Water }

    public struct RegionData
    {
        public int Chunk;
        public MoveClass Class;
        public int TileCount;
        public int2 Center;     // approximate center (pathfinding heuristic)
        public int IslandId;
    }

    // Regions: 4-connected same-class tile sets inside one chunk. Islands: connected region sets.
    // Shallow water is walkable and water, so it belongs to both layers.
    public sealed class RegionGraph : IDisposable
    {
        public const int NoRegion = -1;
        const int ChunkTiles = WorldMap.ChunkSize * WorldMap.ChunkSize;

        readonly WorldMap _map;
        public NativeArray<int> LandRegion;
        public NativeArray<int> WaterRegion;

        RegionData[] _regions = new RegionData[1024];
        bool[] _alive = new bool[1024];
        List<int>[] _edges = new List<int>[1024];
        int[] _parent = new int[1024];
        int[] _islandOfRoot = new int[1024];
        int _used;
        readonly Stack<int> _free = new Stack<int>();
        readonly List<int>[] _chunkRegions;
        readonly int[] _stack = new int[ChunkTiles];
        int _scanCursor;

        public RegionGraph(WorldMap map)
        {
            _map = map;
            LandRegion = new NativeArray<int>(map.TileCount, Allocator.Persistent);
            WaterRegion = new NativeArray<int>(map.TileCount, Allocator.Persistent);
            for (int i = 0; i < map.TileCount; i++)
            {
                LandRegion[i] = NoRegion;
                WaterRegion[i] = NoRegion;
            }
            _chunkRegions = new List<int>[map.ChunkCount];
            for (int c = 0; c < _chunkRegions.Length; c++) _chunkRegions[c] = new List<int>(8);
        }

        public int RegionCount { get; private set; }
        public int IslandCount { get; private set; }
        public int Version { get; private set; } // bumped whenever region/island ids may have changed
        public int RegionCapacity => _used;

        public bool IsAlive(int region) => region >= 0 && region < _used && _alive[region];
        public RegionData GetRegion(int region) => _regions[region];
        public IReadOnlyList<int> GetEdges(int region) => _edges[region];

        public int RegionAt(int x, int y, MoveClass cls)
        {
            int i = _map.Index(x, y);
            return cls == MoveClass.Land ? LandRegion[i] : WaterRegion[i];
        }

        public int IslandAt(int x, int y, MoveClass cls)
        {
            int r = RegionAt(x, y, cls);
            return r < 0 ? -1 : _regions[r].IslandId;
        }

        public bool SameIsland(int x0, int y0, int x1, int y1, MoveClass cls)
        {
            int a = IslandAt(x0, y0, cls);
            return a >= 0 && a == IslandAt(x1, y1, cls);
        }

        public int IslandOfRegion(int region) => IsAlive(region) ? _regions[region].IslandId : -1;

        public void RebuildAll()
        {
            for (int c = 0; c < _map.ChunkCount; c++)
            {
                _map.ClearDirty(c, DirtyMask.Regions);
                RebuildChunk(c);
            }
            RecomputeIslands();
        }

        // Rebuilds at most `budget` chunks flagged Dirty.Regions (round robin). Returns the number rebuilt.
        public int RebuildDirty(int budget)
        {
            int rebuilt = 0;
            int n = _map.ChunkCount;
            for (int k = 0; k < n && rebuilt < budget; k++)
            {
                int c = (_scanCursor + k) % n;
                if ((_map.Chunks[c].Dirty & DirtyMask.Regions) == 0) continue;
                _map.ClearDirty(c, DirtyMask.Regions);
                RebuildChunk(c);
                rebuilt++;
                if (rebuilt == budget) _scanCursor = (c + 1) % n;
            }
            if (rebuilt > 0) RecomputeIslands();
            return rebuilt;
        }

        public void RebuildChunk(int chunk)
        {
            int cx = chunk % _map.ChunksX, cy = chunk / _map.ChunksX;
            int x0 = cx * WorldMap.ChunkSize, y0 = cy * WorldMap.ChunkSize;
            int x1 = x0 + WorldMap.ChunkSize, y1 = y0 + WorldMap.ChunkSize;

            // 1. Release the chunk's old regions and their edges.
            var owned = _chunkRegions[chunk];
            for (int k = 0; k < owned.Count; k++)
            {
                int r = owned[k];
                var edges = _edges[r];
                for (int e = 0; e < edges.Count; e++) _edges[edges[e]].Remove(r);
                edges.Clear();
                _alive[r] = false;
                _free.Push(r);
                RegionCount--;
            }
            owned.Clear();

            for (int y = y0; y < y1; y++)
            {
                int row = y * _map.Width;
                for (int x = x0; x < x1; x++)
                {
                    LandRegion[row + x] = NoRegion;
                    WaterRegion[row + x] = NoRegion;
                }
            }

            // 2. Flood fill each movement class.
            FillClass(chunk, MoveClass.Land, (ushort)TileFlags.Walkable, LandRegion, x0, y0, x1, y1);
            FillClass(chunk, MoveClass.Water, (ushort)TileFlags.Water, WaterRegion, x0, y0, x1, y1);

            // 3. Edges to regions in neighbouring chunks.
            LinkBorders(LandRegion, x0, y0, x1, y1, cx, cy);
            LinkBorders(WaterRegion, x0, y0, x1, y1, cx, cy);

            var data = _map.Chunks[chunk];
            data.FirstRegion = -1;
            data.RegionCount = owned.Count;
            _map.Chunks[chunk] = data;
        }

        void FillClass(int chunk, MoveClass cls, ushort mask, NativeArray<int> layer, int x0, int y0, int x1, int y1)
        {
            int w = _map.Width;
            var flags = _map.Flags;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    int start = y * w + x;
                    if ((flags[start] & mask) == 0 || layer[start] != NoRegion) continue;

                    int id = AllocRegion();
                    _chunkRegions[chunk].Add(id);
                    layer[start] = id;
                    int top = 0;
                    _stack[top++] = start;
                    int count = 0;
                    long sx = 0, sy = 0;

                    while (top > 0)
                    {
                        int i = _stack[--top];
                        int tx = i % w, ty = i / w;
                        count++;
                        sx += tx;
                        sy += ty;
                        if (tx > x0) Visit(i - 1, mask, layer, id, ref top);
                        if (tx < x1 - 1) Visit(i + 1, mask, layer, id, ref top);
                        if (ty > y0) Visit(i - w, mask, layer, id, ref top);
                        if (ty < y1 - 1) Visit(i + w, mask, layer, id, ref top);
                    }

                    _regions[id] = new RegionData
                    {
                        Chunk = chunk,
                        Class = cls,
                        TileCount = count,
                        Center = new int2((int)(sx / count), (int)(sy / count)),
                        IslandId = -1,
                    };
                }
            }
        }

        void Visit(int i, ushort mask, NativeArray<int> layer, int id, ref int top)
        {
            if ((_map.Flags[i] & mask) == 0 || layer[i] != NoRegion) return;
            layer[i] = id;
            _stack[top++] = i;
        }

        void LinkBorders(NativeArray<int> layer, int x0, int y0, int x1, int y1, int cx, int cy)
        {
            int w = _map.Width;
            if (cx > 0)
                for (int y = y0; y < y1; y++) AddEdge(layer[y * w + x0], layer[y * w + x0 - 1]);
            if (cx < _map.ChunksX - 1)
                for (int y = y0; y < y1; y++) AddEdge(layer[y * w + x1 - 1], layer[y * w + x1]);
            if (cy > 0)
                for (int x = x0; x < x1; x++) AddEdge(layer[y0 * w + x], layer[(y0 - 1) * w + x]);
            if (cy < _map.ChunksY - 1)
                for (int x = x0; x < x1; x++) AddEdge(layer[(y1 - 1) * w + x], layer[y1 * w + x]);
        }

        void AddEdge(int a, int b)
        {
            if (a < 0 || b < 0 || a == b) return;
            var ea = _edges[a];
            if (ea.Contains(b)) return;
            ea.Add(b);
            _edges[b].Add(a);
        }

        int AllocRegion()
        {
            int id;
            if (_free.Count > 0)
            {
                id = _free.Pop();
            }
            else
            {
                if (_used == _regions.Length) Grow();
                id = _used++;
                if (_edges[id] == null) _edges[id] = new List<int>(4);
            }
            _alive[id] = true;
            RegionCount++;
            return id;
        }

        void Grow()
        {
            int size = _regions.Length * 2;
            Array.Resize(ref _regions, size);
            Array.Resize(ref _alive, size);
            Array.Resize(ref _edges, size);
            Array.Resize(ref _parent, size);
            Array.Resize(ref _islandOfRoot, size);
        }

        // ponytail: full union-find over all regions after any rebuild; O(regions + edges), a few thousand on Titanic.
        void RecomputeIslands()
        {
            for (int r = 0; r < _used; r++) _parent[r] = r;
            for (int r = 0; r < _used; r++)
            {
                if (!_alive[r]) continue;
                var edges = _edges[r];
                for (int e = 0; e < edges.Count; e++) Union(r, edges[e]);
            }

            for (int r = 0; r < _used; r++) _islandOfRoot[r] = -1;
            int islands = 0;
            for (int r = 0; r < _used; r++)
            {
                if (!_alive[r]) continue;
                int root = Find(r);
                if (_islandOfRoot[root] < 0) _islandOfRoot[root] = islands++;
                _regions[r].IslandId = _islandOfRoot[root];
            }

            Version++;
            int old = IslandCount;
            IslandCount = islands;
            if (old != islands) _map.Events?.Publish(new IslandsChangedEvent(old, islands));
        }

        int Find(int r)
        {
            while (_parent[r] != r)
            {
                _parent[r] = _parent[_parent[r]];
                r = _parent[r];
            }
            return r;
        }

        void Union(int a, int b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra == rb) return;
            if (ra < rb) _parent[rb] = ra;
            else _parent[ra] = rb;
        }

        public void Dispose()
        {
            if (LandRegion.IsCreated) LandRegion.Dispose();
            if (WaterRegion.IsCreated) WaterRegion.Dispose();
        }
    }
}
