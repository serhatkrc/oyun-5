using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using Unity.Mathematics;

namespace PG.Powers
{
    // Ops are built once per PowerCommandSystem; nature-backed ops read the live NatureState through this holder.
    public sealed class NatureRef
    {
        public NatureState Value;
    }

    // Powers applied once at the pointer (clouds, biome seeds) instead of per brushed tile.
    public interface IPointPowerOp
    {
        void Apply(in SimContext ctx, int x, int y, ref SimRandom rng);
    }

    public static class PowerOps
    {
        public const int DefaultDropInterval = 6; // ticks between drops while held

        public static bool IsSupported(PowerDef def, ContentDB db) =>
            TileBrushOps.Create(def, db, null) != null || CreatePoint(def, db) != null;

        // Ticks between repeated applications while held; -1 = once per press.
        public static int IntervalTicks(PowerDef def, ContentDB db)
        {
            if (CreatePoint(def, db) != null) return -1;
            float interval = def.ParamNumber("interval", 0f);
            if (def.Type == "drop" && interval <= 0f) interval = DefaultDropInterval;
            return (int)interval;
        }

        public static IPointPowerOp CreatePoint(PowerDef def, ContentDB db)
        {
            if (def.Type == "spawn" && db.Species.TryGet(def.ParamString("species"), out var species))
                return new SpawnUnitOp(species.Index, (int)math.max(1f, def.ParamNumber("count", 1f)));
            string spawn = def.ParamString("spawn");
            if (string.IsNullOrEmpty(spawn)) return null;
            if (def.Type == "spawn" && db.Clouds.TryGet(spawn, out var cloud)) return new CloudOp(cloud.Index);
            if (def.Type == "drop" && spawn == "seed" && db.Biomes.TryGet(def.ParamString("biome"), out var biome))
                return new SeedOp(biome.MapValue);
            return null;
        }

        // Drop ops (type "drop", per tile under the brush).
        internal static ITileBrushOp CreateDrop(PowerDef def, ContentDB db, NatureRef nature)
        {
            float density = def.ParamNumber("density", 1f);
            string spawn = def.ParamString("spawn");
            if (spawn == "biome_tree") return new BiomeTreeOp(db, density);
            if (!string.IsNullOrEmpty(spawn) && db.Features.TryGet(spawn, out var feature))
                return new FeatureOp(feature.MapValue, feature.KindCode == FeatureDef.KindCrop ? db.Tiles.IdOrDefault("tile.field") : -1, density);
            if (!string.IsNullOrEmpty(spawn) && db.Tiles.TryGet(spawn, out var tile)) return new DryGroundOp(tile.NumericId);

            float fire = def.ParamNumber("fire", 0f);
            if (fire > 0f && string.IsNullOrEmpty(def.ParamString("explosion")) && def.ParamNumber("radius", 0f) <= 0f && def.ParamNumber("dmg", 0f) <= 0f)
                return new FireOp(nature, (byte)math.clamp(fire, 1f, 255f));
            if (def.ParamNumber("freezeWater", 0f) > 0f) return new SnowOp(nature);
            return null;
        }

        // Brush ops of Bölüm 2 (extinguish, freeze, cool_lava).
        internal static ITileBrushOp CreateNatureBrush(string op, NatureRef nature)
        {
            switch (op)
            {
                case "extinguish": return new ExtinguishOp(nature);
                case "freeze": return new FreezeOp(nature);
                case "cool_lava": return new CoolLavaOp(nature);
                default: return null;
            }
        }

        sealed class FeatureOp : ITileBrushOp
        {
            readonly ushort _feature;
            readonly int _requiredGround;
            readonly float _density;

            public FeatureOp(ushort feature, int requiredGround, float density)
            {
                _feature = feature;
                _requiredGround = requiredGround;
                _density = density;
            }

            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng)
            {
                int i = b.Map.Index(x, y);
                if (b.Map.Feature[i] != 0 || (_requiredGround >= 0 && b.Map.Ground[i] != _requiredGround)) return;
                if (_density < 1f && !rng.Chance(_density)) return;
                b.SetFeature(x, y, _feature, 1);
            }
        }

        sealed class BiomeTreeOp : ITileBrushOp
        {
            readonly ContentDB _db;
            readonly float _density;

            public BiomeTreeOp(ContentDB db, float density)
            {
                _db = db;
                _density = density;
            }

            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng)
            {
                int i = b.Map.Index(x, y);
                byte biome = b.Map.Biome[i];
                if (biome == 0 || b.Map.Feature[i] != 0) return;
                var trees = _db.Biomes[biome - 1].TreeValues;
                if (trees.Length == 0 || (_density < 1f && !rng.Chance(_density))) return;
                b.SetFeature(x, y, trees[rng.Range(0, trees.Length)], 1);
            }
        }

        // Lava, goo: only on dry ground.
        sealed class DryGroundOp : ITileBrushOp
        {
            readonly byte _tile;
            public DryGroundOp(byte tile) => _tile = tile;

            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng)
            {
                if (!b.Map.IsWater(x, y)) b.SetGround(x, y, _tile);
            }
        }

        sealed class FireOp : ITileBrushOp
        {
            readonly NatureRef _nature;
            readonly byte _intensity;

            public FireOp(NatureRef nature, byte intensity)
            {
                _nature = nature;
                _intensity = intensity;
            }

            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng) => _nature.Value?.Ignite(x, y, _intensity);
        }

        sealed class SnowOp : ITileBrushOp
        {
            readonly NatureRef _nature;
            public SnowOp(NatureRef nature) => _nature = nature;
            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng) => _nature.Value?.SnowDrop(x, y, ref rng);
        }

        sealed class ExtinguishOp : ITileBrushOp
        {
            readonly NatureRef _nature;
            public ExtinguishOp(NatureRef nature) => _nature = nature;
            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng) => _nature.Value?.Douse(x, y);
        }

        sealed class FreezeOp : ITileBrushOp
        {
            readonly NatureRef _nature;
            public FreezeOp(NatureRef nature) => _nature = nature;
            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng) => _nature.Value?.Freeze(x, y);
        }

        sealed class CoolLavaOp : ITileBrushOp
        {
            readonly NatureRef _nature;
            public CoolLavaOp(NatureRef nature) => _nature = nature;
            public void Apply(TileEditBatch b, int x, int y, ref SimRandom rng) => _nature.Value?.CoolLava(x, y);
        }

        sealed class CloudOp : IPointPowerOp
        {
            readonly int _cloud;
            public CloudOp(int cloud) => _cloud = cloud;

            public void Apply(in SimContext ctx, int x, int y, ref SimRandom rng) =>
                ctx.Nature.SpawnCloud(_cloud, new float2(x + 0.5f, y + 0.5f), ref rng);
        }

        // Creature powers: adults of the species at the pointer (random sex), where they can stand.
        sealed class SpawnUnitOp : IPointPowerOp
        {
            readonly int _species, _count;

            public SpawnUnitOp(int species, int count)
            {
                _species = species;
                _count = count;
            }

            public void Apply(in SimContext ctx, int x, int y, ref SimRandom rng)
            {
                var units = ctx.Units;
                var sp = ctx.Content.Species[_species];
                var mob = sp.Habitat == Habitat.Air ? Mobility.Fly : sp.Habitat == Habitat.Water ? Mobility.Water :
                          sp.Habitat == Habitat.Amph ? Mobility.Amphibious : Mobility.Land;
                if (!units.Paths.NearestStandable(new int2(x, y), mob, out int2 at)) return;
                float lifespan = sp.BaseStats[(int)StatId.Lifespan];
                for (int n = 0; n < _count && units.Store.Count < UnitWorld.HardUnitCap; n++)
                    units.Store.Spawn(new UnitSpawnRequest
                    {
                        Species = _species,
                        Pos = (float2)at + 0.5f + new float2(rng.Range(-0.3f, 0.3f), rng.Range(-0.3f, 0.3f)),
                        AgeYears = lifespan > 0f ? lifespan * 0.25f : 5f,
                        Sex = -1, Mother = EntityId.None, Father = EntityId.None, Subspecies = -1,
                    }, ctx.Clock.Tick, ref rng);
            }
        }

        // Bölüm 2.3: paints radius 2 at once and leaves a seed that boosts spreading for 5 years.
        sealed class SeedOp : IPointPowerOp
        {
            public const int PaintRadius = 2;
            readonly byte _biome;
            public SeedOp(byte biome) => _biome = biome;

            public void Apply(in SimContext ctx, int x, int y, ref SimRandom rng)
            {
                var map = ctx.World;
                using (var b = map.BeginBatch(ChangeSource.Power))
                {
                    for (int dy = -PaintRadius; dy <= PaintRadius; dy++)
                        for (int dx = -PaintRadius; dx <= PaintRadius; dx++)
                            if (dx * dx + dy * dy <= PaintRadius * PaintRadius + PaintRadius && map.InBounds(x + dx, y + dy))
                                b.SetBiome(x + dx, y + dy, _biome);
                }
                if (map.InBounds(x, y)) ctx.Nature.AddSeed(x, y, _biome);
            }
        }
    }

    // Bölüm 2.13: the Sim decides, this runs the matching power at the chosen tile.
    public sealed class DisasterExecutor
    {
        public const int DisasterBrushRadius = 8;

        readonly PowerCommandQueue _queue;
        readonly ContentDB _content;
        readonly GameClock _clock;

        public DisasterExecutor(EventBus events, PowerCommandQueue queue, ContentDB content, GameClock clock)
        {
            _queue = queue;
            _content = content;
            _clock = clock;
            events.Subscribe<DisasterRequestEvent>(OnDisaster);
        }

        void OnDisaster(DisasterRequestEvent e)
        {
            if (e.Power < 0 || !PowerOps.IsSupported(_content.Powers[e.Power], _content)) return; // handlers arrive in Bölüm 7
            _queue.Enqueue(new PowerCommand
            {
                PowerId = (ushort)e.Power,
                From = new int2(e.X, e.Y),
                To = new int2(e.X, e.Y),
                Brush = new BrushSpec(BrushShape.Circle, DisasterBrushRadius),
                Tick = _clock.Tick,
            });
        }
    }
}
