using System;

namespace PG.Core
{
    // PCG32 (XSH RR). One stream per system so adding calls in one system never shifts another.
    public struct SimRandom
    {
        ulong _state;
        ulong _inc;

        public SimRandom(ulong seed, ulong stream)
        {
            _state = 0;
            _inc = (stream << 1) | 1;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public ulong State => _state;
        public ulong Increment => _inc;

        public static SimRandom FromState(ulong state, ulong increment)
        {
            var r = default(SimRandom);
            r._state = state;
            r._inc = increment | 1;
            return r;
        }

        // Local generator for parallel jobs: a shared stream is never used across threads.
        public static SimRandom Derive(ulong worldSeed, int index, long tick)
        {
            ulong seed = worldSeed ^ ((ulong)(uint)index * 0x9E3779B97F4A7C15UL) ^ ((ulong)tick * 0xC2B2AE3D27D4EB4FUL);
            return new SimRandom(seed, (ulong)(uint)index);
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * 6364136223846793005UL + _inc;
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        public int Range(int minIncl, int maxExcl)
        {
            if (maxExcl <= minIncl) return minIncl;
            return minIncl + (int)(NextUInt() % (uint)(maxExcl - minIncl));
        }

        public float Range(float minIncl, float maxExcl) => minIncl + (maxExcl - minIncl) * Value01();

        public float Value01() => (NextUInt() >> 8) * (1f / 16777216f);

        public bool Chance(float p) => Value01() < p;

        // Weighted pick; returns -1 when every weight is <= 0.
        public int WeightedIndex(ReadOnlySpan<float> weights)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
                if (weights[i] > 0f) sum += weights[i];
            if (sum <= 0f) return -1;

            float r = Value01() * sum;
            int last = -1;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f) continue;
                last = i;
                r -= weights[i];
                if (r < 0f) return i;
            }
            return last;
        }
    }
}
