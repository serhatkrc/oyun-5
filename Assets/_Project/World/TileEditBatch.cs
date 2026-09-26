using System;
using PG.Content;

namespace PG.World
{
    // using (var batch = map.BeginBatch(src)) { ... } — dirty chunks are published once on Dispose.
    public readonly struct TileEditBatch : IDisposable
    {
        public readonly WorldMap Map;
        public readonly ChangeSource Source;

        internal TileEditBatch(WorldMap map, ChangeSource source)
        {
            Map = map;
            Source = source;
        }

        public void SetGround(int x, int y, byte type) => Map.SetGround(x, y, type, Source);
        public void SetBiome(int x, int y, byte biome) => Map.SetBiome(x, y, biome, Source);
        public void SetFeature(int x, int y, ushort feature, int stage = 0) => Map.SetFeature(x, y, feature, stage, Source);
        public void SetFlag(int x, int y, TileFlags flag, bool value) => Map.SetFlag(x, y, flag, value);
        public void RaiseLevel(int x, int y) => Map.RaiseLevel(x, y, Source);
        public void LowerLevel(int x, int y) => Map.LowerLevel(x, y, Source);

        public void Dispose() => Map?.EndBatch();
    }
}
