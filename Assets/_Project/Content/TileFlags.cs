using System;

namespace PG.Content
{
    [Flags]
    public enum TileFlags : ushort
    {
        None = 0,
        Walkable = 1 << 0,   // derived from the tile type (cache)
        Water = 1 << 1,
        DeepWater = 1 << 2,
        Burnable = 1 << 3,
        Buildable = 1 << 4,
        SnowCover = 1 << 5,  // snow cover (flag, not a layer)
        Frozen = 1 << 6,     // frozen water
        Burning = 1 << 7,
        Road = 1 << 8,
        Wall = 1 << 9,
        Irradiated = 1 << 10, // after an atomic bomb (Bölüm 7)
        Blessed = 1 << 11,
        Reserved = 1 << 12,   // reserved for a building foundation
        Door = 1 << 13,       // the one walkable tile of a building footprint (Bölüm 5.4)
        // 14-15 free

        TypeMask = Walkable | Water | DeepWater | Burnable | Buildable,
        MoveMask = Walkable | Water,
    }
}
