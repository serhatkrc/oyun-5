using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 2.12
    public sealed class EraSystem : ISimSystem
    {
        public const float TintBlendYears = 2f;

        readonly float[] _weights;

        public EraSystem(ContentDB content) => _weights = new float[content.Eras.Count];

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 0;

        public void Tick(in SimContext ctx)
        {
            var nature = ctx.Nature;
            var era = nature.Era;
            if (era.TintBlend < 1f) era.TintBlend = math.min(1f, era.TintBlend + 1f / (TintBlendYears * SimConst.TicksPerYear));

            if (ctx.Clock.IsYearStart && ctx.Clock.Tick > 0 && !era.Frozen && ctx.Clock.Year - era.StartedYear >= era.DurationYears)
                Advance(nature, ctx.Clock.Year, ctx.Events, ref ctx.Rng.Get(RngStream.Meta));
            else if (ctx.Clock.IsMonthStart)
                nature.Mods = NatureModifiers.From(nature.CurrentEra);
        }

        void Advance(NatureState nature, int year, EventBus events, ref SimRandom rng)
        {
            var era = nature.Era;
            era.CurrentSlot = (era.CurrentSlot + 1) % EraState.SlotCount;
            int next = era.ClockSlots[era.CurrentSlot] - 1;
            if (next < 0 || !era.Enabled[next] || next == era.CurrentEra)
            {
                // Empty (or unusable) slot: weighted pick from the enabled pool by rate; never the same era twice.
                for (int i = 0; i < _weights.Length; i++)
                    _weights[i] = era.Enabled[i] && i != era.CurrentEra ? math.max(0, nature.Content.Eras[i].Rate) : 0f;
                next = rng.WeightedIndex(_weights);
                if (next < 0) next = era.CurrentEra; // only one era enabled: it simply continues
            }
            SetEra(nature, next, year, events, ref rng);
        }

        // Player command (MetaCommand, Bölüm 8) or advance.
        public static void SetEra(NatureState nature, int index, int year, EventBus events, ref SimRandom rng)
        {
            var era = nature.Era;
            var def = nature.Content.Eras[index];
            int old = era.CurrentEra;
            era.PreviousEra = old;
            era.CurrentEra = index;
            era.StartedYear = year;
            era.DurationYears = rng.Range(def.MinYears, def.MaxYears + 1);
            era.TintBlend = old == index ? 1f : 0f;
            nature.Mods = NatureModifiers.From(def);
            nature.NextCloudMonth = 0; // the new era's interval applies from the next month
            if (old != index) events?.Publish(new EraChangedEvent(old, index));
        }
    }

    // Bölüm 2.13: decides once a year, publishes DisasterRequestEvent; Powers executes it.
    public sealed class DisasterSystem : ISimSystem
    {
        static readonly float[] SliderMul = { 0f, 1f, 2f, 3.5f };
        const int PlacementTries = 256;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 10;

        public Func<int> Population = () => 0; // census arrives with Bölüm 3

        public void Tick(in SimContext ctx)
        {
            if (!ctx.Clock.IsYearStart || ctx.Clock.Tick == 0) return;
            var nature = ctx.Nature;
            if (!nature.Laws.IsOn(nature.LawAutoDisasters)) return;
            float lawMul = SliderMul[math.clamp((int)math.round(nature.Laws.Value(nature.LawAutoDisasters)), 0, 3)];
            if (lawMul <= 0f) return;

            ref var rng = ref ctx.Rng.Get(RngStream.Disasters);
            int year = ctx.Clock.Year;
            int population = Population();
            var content = ctx.Content;
            for (int d = 0; d < content.Disasters.Count; d++)
            {
                var def = content.Disasters[d];
                if (year < def.MinWorldAge || population < def.MinPopulation) continue;
                int last = nature.DisasterLastYear[d];
                if (last != int.MinValue && year - last < def.CooldownYears) continue;
                float p = def.ChancePerYear * def.EraMultiplierByIndex[nature.Era.CurrentEra] * lawMul;
                if (!rng.Chance(p)) continue;
                if (!Place(ctx.World, def, ref rng, out int2 at)) continue;
                nature.DisasterLastYear[d] = year;
                ctx.Events.Publish(new DisasterRequestEvent(d, at.x, at.y, def.PowerIndex));
            }
        }

        // ponytail: sampled placement (256 random tiles), not an exact map scan; cities arrive in Bölüm 5.
        static bool Place(WorldMap map, DisasterDef def, ref SimRandom rng, out int2 at)
        {
            switch (def.Placement)
            {
                case "highestMountain": return Highest(map, ref rng, out at);
                case "randomSummit": return Highest(map, ref rng, out at);
                case "coastCity": return Coast(map, ref rng, out at);
                default:
                    if (def.PlacementBiome >= 0 && Sample(map, ref rng, def.PlacementBiome + 1, out at)) return true;
                    return Sample(map, ref rng, -1, out at);
            }
        }

        // biome < 0: any dry tile; otherwise a tile of that biome map value.
        static bool Sample(WorldMap map, ref SimRandom rng, int biome, out int2 at)
        {
            for (int t = 0; t < PlacementTries; t++)
            {
                int i = rng.Range(0, map.TileCount);
                if (biome >= 0 ? map.Biome[i] != biome : (map.Flags[i] & (ushort)TileFlags.Water) != 0) continue;
                at = new int2(i % map.Width, i / map.Width);
                return true;
            }
            at = default;
            return false;
        }

        static bool Highest(WorldMap map, ref SimRandom rng, out int2 at)
        {
            int best = -1;
            at = default;
            for (int t = 0; t < PlacementTries; t++)
            {
                int i = rng.Range(0, map.TileCount);
                int level = map.Tables.Level[map.Ground[i]];
                if (level <= best) continue;
                best = level;
                at = new int2(i % map.Width, i / map.Width);
            }
            return best >= 0 && !map.IsWater(at.x, at.y);
        }

        static bool Coast(WorldMap map, ref SimRandom rng, out int2 at)
        {
            for (int t = 0; t < PlacementTries; t++)
            {
                int i = rng.Range(0, map.TileCount);
                int x = i % map.Width, y = i / map.Width;
                if (map.IsWater(x, y)) continue;
                for (int d = 0; d < 4; d++)
                {
                    var n = NatureSampling.Neighbours4[d];
                    if (!map.InBounds(x + n.x, y + n.y) || !map.IsWater(x + n.x, y + n.y)) continue;
                    at = new int2(x, y);
                    return true;
                }
            }
            return Sample(map, ref rng, -1, out at);
        }
    }
}
