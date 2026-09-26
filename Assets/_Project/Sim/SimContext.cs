using PG.Content;
using PG.Core;
using PG.World;

namespace PG.Sim
{
    // Fixed per-tick order. New systems join a phase; the order never changes (determinism).
    public enum SimPhase
    {
        InputCommands = 0, // player power commands (Bölüm 7)
        World = 1,         // tile changes, fire, lava, biome spread (Bölüm 1-2)
        Climate = 2,       // temperature, clouds, rain (Bölüm 2)
        UnitsThink = 3,    // AI decisions, parallel read-only (Bölüm 3)
        UnitsAct = 4,      // movement, combat (Bölüm 3)
        Civ = 5,           // cities, construction, jobs (Bölüm 5)
        Meta = 6,          // kingdoms, diplomacy, plots (Bölüm 6)
        EventsFlush = 7,   // event dispatch, history
        RegionRebuild = 8, // dirty regions (budgeted)
    }

    public interface ISimSystem
    {
        SimPhase Phase { get; }
        int Order { get; } // order inside the phase
        void Tick(in SimContext ctx);
    }

    public readonly ref struct SimContext
    {
        public readonly WorldMap World;
        public readonly GameClock Clock;
        public readonly EventBus Events;
        public readonly ContentDB Content;
        public readonly SimRandomProvider Rng;
        public readonly RegionGraph Regions;
        public readonly NatureState Nature;
        public readonly UnitWorld Units;

        public SimContext(WorldMap world, GameClock clock, EventBus events, ContentDB content, SimRandomProvider rng, RegionGraph regions,
                          NatureState nature, UnitWorld units)
        {
            Nature = nature;
            Units = units;
            World = world;
            Clock = clock;
            Events = events;
            Content = content;
            Rng = rng;
            Regions = regions;
        }
    }
}
