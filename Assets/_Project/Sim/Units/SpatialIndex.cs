using System;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 3.3: units bucketed by zone (8x8 tiles), rebuilt each tick with a counting sort.
    // Unlike a parallel multi-hash-map the bucket order is the Alive order, so queries are deterministic.
    public sealed class SpatialIndex : IDisposable
    {
        public NativeArray<int> CellStart;  // cells + 1
        public NativeArray<int> Sorted;     // unit indices grouped by cell
        NativeArray<int> _cellOf;           // per alive entry
        readonly int _cellsX, _cellsY;

        public SpatialIndex(WorldMap map)
        {
            _cellsX = map.ZonesX;
            _cellsY = map.ZonesY;
            CellStart = new NativeArray<int>(_cellsX * _cellsY + 1, Allocator.Persistent);
            Sorted = new NativeArray<int>(1024, Allocator.Persistent);
            _cellOf = new NativeArray<int>(1024, Allocator.Persistent);
        }

        public int CellsX => _cellsX;
        public int CellsY => _cellsY;

        public int CellOf(float2 pos)
        {
            int cx = math.clamp((int)math.floor(pos.x) >> WorldMap.ZoneShift, 0, _cellsX - 1);
            int cy = math.clamp((int)math.floor(pos.y) >> WorldMap.ZoneShift, 0, _cellsY - 1);
            return cy * _cellsX + cx;
        }

        public void Rebuild(UnitStore units)
        {
            int n = units.Alive.Length;
            if (Sorted.Length < n)
            {
                int cap = math.ceilpow2(n);
                Sorted.Dispose();
                _cellOf.Dispose();
                Sorted = new NativeArray<int>(cap, Allocator.Persistent);
                _cellOf = new NativeArray<int>(cap, Allocator.Persistent);
            }
            for (int c = 0; c < CellStart.Length; c++) CellStart[c] = 0;
            for (int k = 0; k < n; k++)
            {
                int cell = CellOf(units.Pos[units.Alive[k]]);
                _cellOf[k] = cell;
                CellStart[cell + 1]++;
            }
            for (int c = 1; c < CellStart.Length; c++) CellStart[c] += CellStart[c - 1];
            // Fill using a moving cursor per cell (CellStart shifted by one, restored afterwards).
            for (int k = 0; k < n; k++)
            {
                int cell = _cellOf[k];
                Sorted[CellStart[cell]++] = units.Alive[k];
            }
            for (int c = CellStart.Length - 1; c > 0; c--) CellStart[c] = CellStart[c - 1];
            CellStart[0] = 0;
        }

        // Visits cells overlapping the square around pos; the caller filters by exact distance.
        public void CellRange(float2 pos, float radius, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = math.max(0, (int)math.floor(pos.x - radius) >> WorldMap.ZoneShift);
            y0 = math.max(0, (int)math.floor(pos.y - radius) >> WorldMap.ZoneShift);
            x1 = math.min(_cellsX - 1, (int)math.floor(pos.x + radius) >> WorldMap.ZoneShift);
            y1 = math.min(_cellsY - 1, (int)math.floor(pos.y + radius) >> WorldMap.ZoneShift);
        }

        public void Dispose()
        {
            if (CellStart.IsCreated) CellStart.Dispose();
            if (Sorted.IsCreated) Sorted.Dispose();
            if (_cellOf.IsCreated) _cellOf.Dispose();
        }
    }
}
