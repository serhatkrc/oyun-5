using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;

namespace PG.Sim
{
    public sealed class SimulationPipeline : IDisposable
    {
        readonly List<ISimSystem> _systems = new List<ISimSystem>();
        readonly List<ProfilerMarker> _markers = new List<ProfilerMarker>();
        readonly List<double> _lastMs = new List<double>();
        readonly Stopwatch _stopwatch = new Stopwatch();

        public int Count => _systems.Count;
        public ISimSystem this[int index] => _systems[index];
        public double LastMs(int index) => _lastMs[index];
        public double LastTickMs { get; private set; }

        // Stable insert by (Phase, Order): equal keys keep registration order.
        public void Add(ISimSystem system)
        {
            int at = _systems.Count;
            for (int i = 0; i < _systems.Count; i++)
            {
                var s = _systems[i];
                if (s.Phase > system.Phase || (s.Phase == system.Phase && s.Order > system.Order))
                {
                    at = i;
                    break;
                }
            }
            _systems.Insert(at, system);
            _markers.Insert(at, new ProfilerMarker("Sim." + system.GetType().Name));
            _lastMs.Insert(at, 0);
        }

        public void Tick(in SimContext ctx)
        {
            double total = 0;
            for (int i = 0; i < _systems.Count; i++) total += Run(i, ctx);
            LastTickMs = total;
        }

        public void RunPhase(in SimContext ctx, SimPhase phase)
        {
            for (int i = 0; i < _systems.Count; i++)
                if (_systems[i].Phase == phase) Run(i, ctx);
        }

        public T Find<T>() where T : class, ISimSystem
        {
            for (int i = 0; i < _systems.Count; i++)
                if (_systems[i] is T t) return t;
            return null;
        }

        public void Dispose()
        {
            for (int i = 0; i < _systems.Count; i++)
                if (_systems[i] is IDisposable d) d.Dispose();
        }

        double Run(int i, in SimContext ctx)
        {
            var marker = _markers[i];
            marker.Begin();
            _stopwatch.Restart();
            _systems[i].Tick(ctx);
            _stopwatch.Stop();
            marker.End();
            double ms = _stopwatch.Elapsed.TotalMilliseconds;
            _lastMs[i] = ms;
            return ms;
        }
    }
}
