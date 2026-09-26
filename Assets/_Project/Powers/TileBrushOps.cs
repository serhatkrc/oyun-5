using System;
using PG.Content;
using PG.Core;
using PG.World;

namespace PG.Powers
{
    // Brush ops built from powers.json params.op (Bölüm 1.13). Ops of later chapters return null from Create.
    public static class TileBrushOps
    {
        // nature: live NatureState holder for Bölüm 2 ops; null only for support checks.
        public static ITileBrushOp Create(PowerDef def, ContentDB db, NatureRef nature)
        {
            if (def.Type == "drop") return PowerOps.CreateDrop(def, db, nature ?? new NatureRef());
            if (def.Type != "brush") return null;
            string opName = def.ParamString("op");
            var natureOp = PowerOps.CreateNatureBrush(opName, nature ?? new NatureRef());
            if (natureOp != null) return natureOp;
            switch (opName)
            {
                case "set": return Tile(def, db, out var set) ? new SetGroundOp(set.NumericId) : null;
                case "raise_to": return Tile(def, db, out var up) ? new RaiseToOp(up.Level) : null;
                case "lower_to": return Tile(def, db, out var down) ? new LowerToOp(down.Level) : null;
                case "raise": return new RaiseOp();
                case "lower": return new LowerOp();
                case "sponge": return new SpongeOp((byte)db.TileByLevel[3]);
                case "flag": return FlagOp.From(def);
                case "erase": return new EraseOp();
                default: return null;
            }
        }

        static bool Tile(PowerDef def, ContentDB db, out TileTypeDef tile) =>
            db.Tiles.TryGet("tile." + def.ParamString("target"), out tile);

        static int Level(TileEditBatch b, int x, int y) => b.Map.Tables.Level[b.Map.Ground[b.Map.Index(x, y)]];

        static bool StepWithin(TileEditBatch b, int x, int y, bool up, int limit)
        {
            var tables = b.Map.Tables;
            byte ground = b.Map.Ground[b.Map.Index(x, y)];
            byte to = up ? tables.RaiseTo[ground] : tables.LowerTo[ground];
            if (to == TileTables.NoStep) return false;
            int level = tables.Level[to];
            return up ? level <= limit : level >= limit;
        }

        sealed class SetGroundOp : ITileBrushOp
        {
            readonly byte _type;
            public SetGroundOp(byte type) => _type = type;
            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng) => b.SetGround(x, y, _type);
        }

        // One level per application toward the limit ("kneading" feel): holding the brush builds up slowly.
        sealed class RaiseToOp : ITileBrushOp
        {
            readonly int _limit;
            public RaiseToOp(int limit) => _limit = limit;

            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng)
            {
                int level = Level(b, x, y);
                // Level-less tiles (field, lava...) step only if their raise target stays within the limit.
                bool raise = level >= 0 ? level < _limit : StepWithin(b, x, y, true, _limit);
                if (raise) b.RaiseLevel(x, y);
            }
        }

        sealed class LowerToOp : ITileBrushOp
        {
            readonly int _limit;
            public LowerToOp(int limit) => _limit = limit;

            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng)
            {
                int level = Level(b, x, y);
                bool lower = level >= 0 ? level > _limit : StepWithin(b, x, y, false, _limit);
                if (lower) b.LowerLevel(x, y);
            }
        }

        sealed class RaiseOp : ITileBrushOp
        {
            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng) => b.RaiseLevel(x, y);
        }

        sealed class LowerOp : ITileBrushOp
        {
            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng) => b.LowerLevel(x, y);
        }

        // Water -> land, shallow first: shallow becomes sand, deeper water rises one level.
        sealed class SpongeOp : ITileBrushOp
        {
            readonly byte _sand;
            public SpongeOp(byte sand) => _sand = sand;

            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng)
            {
                var map = b.Map;
                var flags = map.GetFlags(x, y);
                if ((flags & TileFlags.Water) == 0) return;
                if ((flags & TileFlags.Walkable) != 0) b.SetGround(x, y, _sand);
                else b.RaiseLevel(x, y);
            }
        }

        sealed class FlagOp : ITileBrushOp
        {
            readonly TileFlags _flags;
            FlagOp(TileFlags flags) => _flags = flags;

            public static FlagOp From(PowerDef def)
            {
                var result = TileFlags.None;
                foreach (var name in def.ParamStrings("flags"))
                    if (Enum.TryParse(name, true, out TileFlags f)) result |= f;
                return result == TileFlags.None ? null : new FlagOp(result);
            }

            // Flags like roads only stick to dry walkable ground.
            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng)
            {
                var flags = b.Map.GetFlags(x, y);
                if ((flags & TileFlags.Water) != 0 || (flags & TileFlags.Walkable) == 0) return;
                b.SetFlag(x, y, _flags, true);
            }
        }

        // Removes biome, plants and built flags; the ground stays.
        sealed class EraseOp : ITileBrushOp
        {
            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng)
            {
                b.SetBiome(x, y, 0);
                b.SetFeature(x, y, 0);
                b.SetFlag(x, y, TileFlags.Road | TileFlags.Wall, false);
            }
        }
    }
}
