using System;
using System.Collections.Generic;

namespace PG.Core
{
    public sealed class IdPool
    {
        int[] _generations;
        readonly Stack<int> _free = new Stack<int>();
        int _count;

        public IdPool(int initialCapacity = 256)
        {
            _generations = new int[Math.Max(1, initialCapacity)];
        }

        public int Capacity => _count;          // highest slot ever used + 1
        public int AliveCount => _count - _free.Count;

        public EntityId Allocate()
        {
            int index;
            if (_free.Count > 0)
            {
                index = _free.Pop();
            }
            else
            {
                if (_count == _generations.Length) Array.Resize(ref _generations, _generations.Length * 2);
                index = _count++;
            }
            return new EntityId(index, _generations[index]);
        }

        // Bumps the generation so stale ids fail IsAlive, then frees the slot.
        public void Release(EntityId id)
        {
            if (!IsAlive(id)) return;
            _generations[id.Index]++;
            _free.Push(id.Index);
        }

        public bool IsAlive(EntityId id) => id.Index >= 0 && id.Index < _count && _generations[id.Index] == id.Generation;
    }
}
