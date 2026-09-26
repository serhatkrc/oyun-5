using System;

namespace PG.Core
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public readonly int Index;      // slot in the store
        public readonly int Generation; // bumped every time the slot is reused

        public static readonly EntityId None = new EntityId(-1, 0);

        public EntityId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public bool IsNone => Index < 0;

        public bool Equals(EntityId other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;
        public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);
        public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);
        public override string ToString() => IsNone ? "None" : $"{Index}:{Generation}";
    }
}
