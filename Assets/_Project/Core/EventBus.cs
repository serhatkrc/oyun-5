using System;
using System.Collections.Generic;

namespace PG.Core
{
    public interface ISimEvent { }

    // Events queue up during a tick and are dispatched in the Events Flush phase.
    // One List<T> per event type (no boxing). Events published by handlers are dispatched on the next flush.
    public sealed class EventBus
    {
        interface IQueue
        {
            int PendingCount { get; }
            void Swap();
            void Dispatch();
            void Clear();
        }

        sealed class Queue<T> : IQueue where T : struct, ISimEvent
        {
            public List<T> Pending = new List<T>(64);
            List<T> _processing = new List<T>(64);
            public readonly List<Action<T>> Handlers = new List<Action<T>>();

            public int PendingCount => Pending.Count;

            public void Swap()
            {
                var t = _processing;
                _processing = Pending;
                Pending = t;
            }

            public void Dispatch()
            {
                for (int e = 0; e < _processing.Count; e++)
                {
                    var evt = _processing[e];
                    for (int h = 0; h < Handlers.Count; h++) Handlers[h](evt);
                }
                _processing.Clear();
            }

            public void Clear()
            {
                Pending.Clear();
                _processing.Clear();
            }
        }

        readonly Dictionary<Type, IQueue> _queues = new Dictionary<Type, IQueue>();
        readonly List<IQueue> _order = new List<IQueue>(); // creation order keeps dispatch deterministic

        public int PendingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _order.Count; i++) n += _order[i].PendingCount;
                return n;
            }
        }

        public void Publish<T>(in T evt) where T : struct, ISimEvent => GetQueue<T>().Pending.Add(evt);

        public void Subscribe<T>(Action<T> handler) where T : struct, ISimEvent => GetQueue<T>().Handlers.Add(handler);

        public void Unsubscribe<T>(Action<T> handler) where T : struct, ISimEvent => GetQueue<T>().Handlers.Remove(handler);

        internal void Flush()
        {
            // Swap every queue first so events raised by handlers wait for the next tick.
            for (int i = 0; i < _order.Count; i++) _order[i].Swap();
            for (int i = 0; i < _order.Count; i++) _order[i].Dispatch();
        }

        public void ClearPending()
        {
            for (int i = 0; i < _order.Count; i++) _order[i].Clear();
        }

        Queue<T> GetQueue<T>() where T : struct, ISimEvent
        {
            if (_queues.TryGetValue(typeof(T), out var q)) return (Queue<T>)q;
            var created = new Queue<T>();
            _queues.Add(typeof(T), created);
            _order.Add(created);
            return created;
        }
    }
}
