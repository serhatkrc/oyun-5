namespace PG.Core
{
    public enum RngStream
    {
        WorldGen,
        Biome,
        Fire,
        Weather,
        UnitAI,
        Combat,
        Civ,
        Meta,
        Disasters,
        Names,
        Powers,
    }

    public sealed class SimRandomProvider
    {
        public const int StreamCount = (int)RngStream.Powers + 1;

        readonly SimRandom[] _streams = new SimRandom[StreamCount];

        public SimRandomProvider(ulong worldSeed)
        {
            WorldSeed = worldSeed;
            for (int i = 0; i < StreamCount; i++)
                _streams[i] = new SimRandom(worldSeed, (ulong)i + 1);
        }

        public ulong WorldSeed { get; }

        public ref SimRandom Get(RngStream stream) => ref _streams[(int)stream];

        // Raw stream access for save/load.
        public SimRandom GetStream(int index) => _streams[index];

        public void SetStream(int index, SimRandom state) => _streams[index] = state;
    }
}
