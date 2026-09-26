using System;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Powers
{
    public enum BrushShape : byte { Circle, Square }

    public readonly struct BrushSpec
    {
        public readonly BrushShape Shape;
        public readonly int Radius;    // 0 = single tile
        public readonly float Density; // 1 = every tile, < 1 = random scatter

        public BrushSpec(BrushShape shape, int radius, float density = 1f)
        {
            Shape = shape;
            Radius = radius;
            Density = density;
        }
    }

    public static class Brushes
    {
        public static readonly int[] Radii = { 0, 1, 2, 3, 5, 8, 12, 20, 32 };

        public static bool Contains(BrushShape shape, int radius, int dx, int dy) =>
            shape == BrushShape.Square || dx * dx + dy * dy <= radius * radius + radius;
    }

    public interface ITileBrushOp
    {
        void Apply(TileEditBatch batch, int x, int y, ref SimRandom rng);
    }

    public static class BrushExecutor
    {
        // Each tile is touched at most once per stroke even where stamps overlap.
        static int[] _mark = Array.Empty<int>();
        static int _strokeId;

        public static void Stroke(WorldMap map, BrushSpec brush, int2 from, int2 to, ITileBrushOp op, ref SimRandom rng,
                                  ChangeSource src = ChangeSource.Power)
        {
            if (_mark.Length != map.TileCount)
            {
                _mark = new int[map.TileCount];
                _strokeId = 0;
            }
            _strokeId++;
            if (_strokeId == int.MaxValue)
            {
                Array.Clear(_mark, 0, _mark.Length);
                _strokeId = 1;
            }

            using (var batch = map.BeginBatch(src))
            {
                int step = Math.Max(1, brush.Radius / 2);
                int dx = Math.Abs(to.x - from.x), dy = -Math.Abs(to.y - from.y);
                int sx = from.x < to.x ? 1 : -1, sy = from.y < to.y ? 1 : -1;
                int err = dx + dy;
                int x = from.x, y = from.y, walked = 0;

                Stamp(batch, x, y, brush, op, ref rng);
                while (x != to.x || y != to.y)
                {
                    int e2 = 2 * err;
                    if (e2 >= dy) { err += dy; x += sx; }
                    if (e2 <= dx) { err += dx; y += sy; }
                    walked++;
                    if (walked % step == 0 || (x == to.x && y == to.y)) Stamp(batch, x, y, brush, op, ref rng);
                }
            }
        }

        static void Stamp(TileEditBatch batch, int cx, int cy, BrushSpec brush, ITileBrushOp op, ref SimRandom rng)
        {
            var map = batch.Map;
            int r = brush.Radius;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (!Brushes.Contains(brush.Shape, r, dx, dy)) continue;
                    int x = cx + dx, y = cy + dy;
                    if (!map.InBounds(x, y)) continue;
                    int i = map.Index(x, y);
                    if (_mark[i] == _strokeId) continue;
                    _mark[i] = _strokeId;
                    if (brush.Density < 1f && !rng.Chance(brush.Density)) continue;
                    op.Apply(batch, x, y, ref rng);
                }
            }
        }
    }
}
