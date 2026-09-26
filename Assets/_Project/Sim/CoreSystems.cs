namespace PG.Sim
{
    public sealed class EventsFlushSystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.EventsFlush;
        public int Order => 0;

        public void Tick(in SimContext ctx) => ctx.Events.Flush();
    }

    public sealed class RegionRebuildSystem : ISimSystem
    {
        public const int ChunksPerTick = 4; // Bölüm 1.9.3 budget

        public SimPhase Phase => SimPhase.RegionRebuild;
        public int Order => 0;
        public int LastRebuilt { get; private set; }

        public void Tick(in SimContext ctx) => LastRebuilt = ctx.Regions.RebuildDirty(ChunksPerTick);
    }
}
