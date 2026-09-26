using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 2.8: walks only the burning list, every 2 ticks.
    public sealed class FireSystem : ISimSystem, IDisposable
    {
        public const float SpreadChance = 0.08f;
        public const float WindWeight = 0.8f;

        NativeList<int> _work = new NativeList<int>(1024, Allocator.Persistent);
        NativeBitArray _seen;

        public SimPhase Phase => SimPhase.World;
        public int Order => 30;

        public void Tick(in SimContext ctx)
        {
            if ((ctx.Clock.Tick & 1) != 0) return;
            var nature = ctx.Nature;
            if (nature.Burning.Length == 0) return;

            var map = ctx.World;
            var t = map.Tables;
            if (!_seen.IsCreated || _seen.Length != map.TileCount)
            {
                if (_seen.IsCreated) _seen.Dispose();
                _seen = new NativeBitArray(map.TileCount, Allocator.Persistent);
            }

            ref var rng = ref ctx.Rng.Get(RngStream.Fire);
            bool spread = nature.Laws.IsOn(nature.LawFireSpread);
            float spreadMul = SpreadChance * nature.Mods.FireSpreadMul;
            float2 wind = nature.Wind;

            // New ignitions during the step append to nature.Burning and are processed next step.
            _work.Clear();
            _work.AddRange(nature.Burning.AsArray());
            nature.Burning.Clear();

            using (map.BeginBatch(ChangeSource.Nature))
            {
                for (int k = 0; k < _work.Length; k++)
                {
                    int i = _work[k];
                    if ((map.Flags[i] & (ushort)TileFlags.Burning) == 0 || _seen.IsSet(i)) continue;
                    _seen.Set(i, true);
                    int x = i % map.Width, y = i / map.Width;

                    int fire = map.Fire[i];
                    if (fire == 0)
                    {
                        map.SetFlag(x, y, TileFlags.Burning, false); // doused: no scorch
                        continue;
                    }

                    ushort feature = map.Feature[i];
                    bool featureFuel = t.FeatureBurnable[feature] != 0;
                    int fuel = (featureFuel ? 2 : 0) + (map.Building[i] >= 0 ? 3 : 0) + ((map.Flags[i] & (ushort)TileFlags.Burnable) != 0 ? 1 : 0);
                    fire = math.min(fire + fuel * 6 - 10, 255);
                    if (fire <= 0)
                    {
                        map.SetFire(x, y, 0);
                        map.SetFlag(x, y, TileFlags.Burning, false);
                        byte burnTo = t.BurnTo[map.Ground[i]];
                        if (burnTo != TileTables.NoStep) map.SetGround(x, y, burnTo, ChangeSource.Nature);
                        continue;
                    }
                    map.SetFire(x, y, (byte)fire);
                    nature.Burning.Add(i);

                    if (featureFuel)
                    {
                        int res = map.GetFeatureResource(i) - 1;
                        if (res > 0) map.SetFeatureResource(x, y, res);
                        else map.SetFeature(x, y, t.FeatureBurnsInto[feature], 3, ChangeSource.Nature);
                    }

                    if (!spread) continue;
                    float heat = fire / 255f;
                    for (int d = 0; d < 4; d++)
                    {
                        var dir = NatureSampling.Neighbours4[d];
                        int nx = x + dir.x, ny = y + dir.y;
                        if (!map.InBounds(nx, ny)) continue;
                        int n = map.Index(nx, ny);
                        if ((map.Flags[n] & (ushort)TileFlags.Burning) != 0 || !nature.CanBurn(n)) continue;
                        float p = spreadMul * (1f + WindWeight * math.dot(wind, dir)) * heat;
                        if (rng.Chance(p)) nature.Ignite(nx, ny, NatureState.IgniteDefault);
                    }
                }
            }

            for (int k = 0; k < _work.Length; k++) _seen.Set(_work[k], false);
        }

        public void Dispose()
        {
            if (_work.IsCreated) _work.Dispose();
            if (_seen.IsCreated) _seen.Dispose();
        }
    }

    // Bölüm 2.9 (lava) and 2.10 goo. Active tiles arrive through WorldMap.ActivatedTiles.
    public sealed class LavaSystem : ISimSystem
    {
        public const int StepTicks = 6;
        public const float FlowChance = 0.25f;
        public const byte LavaIgnite = 120;
        public const int GooPerTick = 16;

        public SimPhase Phase => SimPhase.World;
        public int Order => 40;

        public void Tick(in SimContext ctx)
        {
            var nature = ctx.Nature;
            var map = ctx.World;
            long tick = ctx.Clock.Tick;
            Drain(nature, map, tick);

            ref var rng = ref ctx.Rng.Get(RngStream.Fire);
            using (map.BeginBatch(ChangeSource.Nature))
            {
                if (tick % StepTicks == 0 && nature.Lava.Length > 0) StepLava(nature, map, tick, ref rng);
                if (nature.Goo.Length > 0 && nature.Laws.IsOn(nature.LawGooSpread)) StepGoo(nature, map, ref rng);
            }
        }

        static void Drain(NatureState nature, WorldMap map, long tick)
        {
            var list = map.ActivatedTiles;
            if (list.Length == 0) return;
            var t = map.Tables;
            for (int k = 0; k < list.Length; k++)
            {
                int i = list[k].x;
                byte old = (byte)list[k].y;
                byte g = map.Ground[i];
                if (t.Active[g] == 0) continue;
                if (t.Spreads[g] != 0 && t.Spreads[old] == 0) nature.Goo.Add(i);
                bool lava = t.Flows[g] != 0 || t.DecayTicks[g] != 0;
                bool wasLava = t.Flows[old] != 0 || t.DecayTicks[old] != 0;
                if (lava && !wasLava)
                    nature.Lava.Add(new LavaCell { Tile = i, Type = g, DueTick = tick + t.DecayTicks[g], BaseLevel = t.ReliefLevel[old] });
                // lava -> lava (decay, quench): the existing cell notices the type change itself
            }
            list.Clear();
        }

        static void StepLava(NatureState nature, WorldMap map, long tick, ref SimRandom rng)
        {
            var t = map.Tables;
            bool cooling = nature.Laws.IsOn(nature.LawLavaCooling);
            var cells = nature.Lava;
            int write = 0;
            int count = cells.Length; // flows append new cells through Drain next tick, not here
            for (int k = 0; k < count; k++)
            {
                var c = cells[k];
                int i = c.Tile;
                byte g = map.Ground[i];
                if (t.Flows[g] == 0 && t.DecayTicks[g] == 0) continue; // cooled into rock or replaced
                if (g != c.Type)
                {
                    c.Type = g;
                    c.DueTick = tick + t.DecayTicks[g];
                }
                if (!cooling) c.DueTick += StepTicks;
                int x = i % map.Width, y = i / map.Width;

                if (t.Flows[g] != 0)
                {
                    if (t.Ignites[g] != 0)
                        for (int d = 0; d < 4; d++)
                        {
                            var dir = NatureSampling.Neighbours4[d];
                            if (map.InBounds(x + dir.x, y + dir.y) && nature.CanBurn(map.Index(x + dir.x, y + dir.y)))
                                nature.Ignite(x + dir.x, y + dir.y, LavaIgnite);
                        }

                    if (rng.Chance(FlowChance))
                    {
                        var dir = NatureSampling.Neighbours4[rng.Range(0, 4)];
                        int nx = x + dir.x, ny = y + dir.y;
                        if (map.InBounds(nx, ny))
                        {
                            byte ng = map.Ground[map.Index(nx, ny)];
                            bool water = (t.TypeFlags[ng] & (ushort)TileFlags.Water) != 0;
                            if (water && t.QuenchTo[g] != TileTables.NoStep)
                            {
                                map.SetGround(x, y, t.QuenchTo[g], ChangeSource.Nature);
                                map.SetGround(nx, ny, t.QuenchTo[g], ChangeSource.Nature);
                                g = t.QuenchTo[g];
                                c.Type = g;
                                c.DueTick = tick + t.DecayTicks[g];
                            }
                            else if (!water && t.Active[ng] == 0 && t.ReliefLevel[ng] < c.BaseLevel && t.Level[ng] >= 0)
                                map.SetGround(nx, ny, g, ChangeSource.Nature);
                        }
                    }
                }

                if (tick >= c.DueTick && t.DecayTo[g] != TileTables.NoStep)
                {
                    byte to = t.DecayTo[g];
                    if (t.DecayToHigh[g] != TileTables.NoStep && c.BaseLevel > t.Level[to]) to = t.DecayToHigh[g];
                    map.SetGround(x, y, to, ChangeSource.Nature);
                    if (t.DecayFeature[g] != 0 && rng.Chance(t.DecayFeatureChance[g]))
                        map.SetFeature(x, y, t.DecayFeature[g], 2, ChangeSource.Nature);
                    if (t.Flows[to] == 0 && t.DecayTicks[to] == 0) continue;
                    c.Type = to;
                    c.DueTick = tick + t.DecayTicks[to];
                }
                cells[write++] = c;
            }
            // keep cells appended during the loop (none today, but cheap to be safe)
            for (int k = count; k < cells.Length; k++) cells[write++] = cells[k];
            cells.Resize(write, NativeArrayOptions.UninitializedMemory);
        }

        static void StepGoo(NatureState nature, WorldMap map, ref SimRandom rng)
        {
            var t = map.Tables;
            for (int s = 0; s < GooPerTick && nature.Goo.Length > 0; s++)
            {
                int k = rng.Range(0, nature.Goo.Length);
                int i = nature.Goo[k];
                byte g = map.Ground[i];
                if (t.Spreads[g] == 0)
                {
                    nature.Goo.RemoveAtSwapBack(k);
                    continue;
                }
                var dir = NatureSampling.Neighbours4[rng.Range(0, 4)];
                int nx = i % map.Width + dir.x, ny = i / map.Width + dir.y;
                if (!map.InBounds(nx, ny)) continue;
                byte ng = map.Ground[map.Index(nx, ny)];
                if ((t.TypeFlags[ng] & (ushort)TileFlags.Water) != 0 || ng == g) continue;
                map.SetGround(nx, ny, g, ChangeSource.Nature);
            }
        }
    }

    // Bölüm 2.10: 256 random tiles per tick.
    public sealed class TileRecoverySystem : ISimSystem
    {
        public const int SamplesPerTick = 256;
        public const float IrradiationFade = 1f / 3000f;

        public SimPhase Phase => SimPhase.World;
        public int Order => 50;

        public void Tick(in SimContext ctx)
        {
            var map = ctx.World;
            var t = map.Tables;
            ref var rng = ref ctx.Rng.Get(RngStream.Biome);
            using (map.BeginBatch(ChangeSource.Nature))
            {
                for (int s = 0; s < SamplesPerTick; s++)
                {
                    int i = rng.Range(0, map.TileCount);
                    int x = i % map.Width, y = i / map.Width;
                    if ((map.Flags[i] & (ushort)TileFlags.Irradiated) != 0 && rng.Chance(IrradiationFade))
                        map.SetFlag(x, y, TileFlags.Irradiated, false);

                    byte g = map.Ground[i];
                    byte to = t.RecoverTo[g];
                    if (to == TileTables.NoStep || !rng.Chance(t.RecoverChance[g])) continue;
                    if (t.RecoverNeedsWater[g] != 0 && !NextToWater(map, x, y)) continue;
                    map.SetGround(x, y, to, ChangeSource.Nature);
                }
            }
        }

        static bool NextToWater(WorldMap map, int x, int y)
        {
            for (int d = 0; d < 4; d++)
            {
                var dir = NatureSampling.Neighbours4[d];
                if (map.InBounds(x + dir.x, y + dir.y) && map.IsWater(x + dir.x, y + dir.y)) return true;
            }
            return false;
        }
    }
}
