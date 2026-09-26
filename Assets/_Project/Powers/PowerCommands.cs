using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.Sim;
using Unity.Mathematics;

namespace PG.Powers
{
    // UI -> simulation: player input never touches the world directly (determinism, replay).
    public struct PowerCommand
    {
        public ushort PowerId;
        public int2 From;
        public int2 To;
        public BrushSpec Brush;
        public long Tick; // clock tick when issued (command log)
    }

    public sealed class PowerCommandQueue
    {
        readonly List<PowerCommand> _pending = new List<PowerCommand>(64);

        public int Count => _pending.Count;

        public void Enqueue(in PowerCommand command) => _pending.Add(command);

        public PowerCommand this[int index] => _pending[index];

        public void Clear() => _pending.Clear();
    }

    // Phase 0: applies queued commands in order.
    public sealed class PowerCommandSystem : ISimSystem
    {
        readonly PowerCommandQueue _queue;
        readonly ITileBrushOp[] _brushOps;
        readonly IPointPowerOp[] _pointOps;
        readonly NatureRef _nature = new NatureRef();

        public PowerCommandSystem(PowerCommandQueue queue, ContentDB content)
        {
            _queue = queue;
            _brushOps = new ITileBrushOp[content.Powers.Count];
            _pointOps = new IPointPowerOp[content.Powers.Count];
            for (int i = 0; i < _brushOps.Length; i++)
            {
                _pointOps[i] = PowerOps.CreatePoint(content.Powers[i], content);
                if (_pointOps[i] == null) _brushOps[i] = TileBrushOps.Create(content.Powers[i], content, _nature);
            }
        }

        public SimPhase Phase => SimPhase.InputCommands;
        public int Order => 0;

        public void Tick(in SimContext ctx)
        {
            if (_queue.Count == 0) return;
            _nature.Value = ctx.Nature;
            ref var rng = ref ctx.Rng.Get(RngStream.Powers);
            for (int i = 0; i < _queue.Count; i++)
            {
                var cmd = _queue[i];
                if (cmd.PowerId >= _brushOps.Length) continue;
                var point = _pointOps[cmd.PowerId];
                if (point != null)
                {
                    if (ctx.World.InBounds(cmd.To.x, cmd.To.y)) point.Apply(ctx, cmd.To.x, cmd.To.y, ref rng);
                    continue;
                }
                var op = _brushOps[cmd.PowerId];
                if (op != null) BrushExecutor.Stroke(ctx.World, cmd.Brush, cmd.From, cmd.To, op, ref rng);
            }
            _queue.Clear();
        }
    }
}
