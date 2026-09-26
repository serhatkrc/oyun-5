using System;
using PG.Content;
using PG.Core;
using PG.World;

namespace PG.Sim
{
    // Everything that makes up one running world. Owned by the game session; disposed when replaced.
    public sealed class SimWorld : IDisposable
    {
        public readonly ContentDB Content;
        public readonly GameClock Clock;
        public readonly EventBus Events;
        public readonly SimRandomProvider Rng;
        public readonly WorldMap World;
        public readonly RegionGraph Regions;
        public readonly NatureState Nature;
        public readonly UnitWorld Units;
        public readonly CivState Civ;
        public readonly DisasterSystem Disasters = new DisasterSystem();
        public readonly SimulationPipeline Pipeline = new SimulationPipeline();

        public SimWorld(ContentDB content, WorldMap world, SimRandomProvider rng, GameClock clock = null, string worldName = null)
        {
            Content = content;
            World = world;
            Rng = rng;
            Clock = clock ?? new GameClock();
            Events = new EventBus();
            World.Events = Events;
            WorldName = string.IsNullOrEmpty(worldName) ? "World" : worldName;

            Regions = new RegionGraph(world);
            Regions.RebuildAll();
            Events.ClearPending(); // the initial build is not news

            Nature = new NatureState(world, content, rng.WorldSeed);
            Units = new UnitWorld(world, Regions, content, Nature.Laws) { Events = Events };
            Disasters.Population = () => Units.Store.Count;
            Civ = new CivState(world, content);
            Civ.Attach(Units);

            Pipeline.Add(new BiomeSpreadSystem());
            Pipeline.Add(new FeatureGrowthSystem());
            Pipeline.Add(new FireSystem());
            Pipeline.Add(new LavaSystem());
            Pipeline.Add(new TileRecoverySystem());
            Pipeline.Add(new TemperatureSystem(content));
            Pipeline.Add(new WindSystem());
            Pipeline.Add(new CloudSystem(content));
            Pipeline.Add(new EraSystem(content));
            Pipeline.Add(Disasters);
            Pipeline.Add(new UnitIndexSystem());
            Pipeline.Add(new UnitThinkSystem());
            Pipeline.Add(new UnitActSystem());
            Pipeline.Add(new ProjectileSystem());
            Pipeline.Add(new UnitLifeSystem());
            Pipeline.Add(new UnitCleanupSystem());
            Pipeline.Add(new AnimalSpawnSystem());
            Pipeline.Add(new SettlementSystem());
            Pipeline.Add(new CivMonthlySystem());
            Pipeline.Add(new CityPlannerSystem());
            Pipeline.Add(new CivYearlySystem());
            Pipeline.Add(new BuildingUpkeepSystem());
            Pipeline.Add(new EventsFlushSystem());
            Pipeline.Add(new RegionRebuildSystem());
        }

        public string WorldName { get; set; }
        public ulong Seed => Rng.WorldSeed;
        public int Population => Units.Store.Count;

        // Worldgen step 10 (Bölüm 3.13): a derived generator so the shared streams stay as a fresh world expects.
        public int SpawnInitialAnimals()
        {
            var rng = SimRandom.Derive(Rng.WorldSeed, 0xA41A, 0);
            return AnimalSpawnSystem.SpawnInitial(Units, World, Content, Clock.Tick, ref rng);
        }

        public SimContext Context => new SimContext(World, Clock, Events, Content, Rng, Regions, Nature, Units);

        public void Tick()
        {
            Pipeline.Tick(Context);
            Clock.AdvanceTick();
        }

        // Player edits while paused: apply queued commands without advancing time.
        public void ApplyInputWhilePaused() => Pipeline.RunPhase(Context, SimPhase.InputCommands);

        public void Dispose()
        {
            Pipeline.Dispose();
            Civ.Dispose();
            Units.Dispose();
            Nature.Dispose();
            Regions.Dispose();
            World.Dispose();
        }
    }
}
