using System;

namespace BlackoutProtocol.AI.Breacher.Logic
{
    /// <summary>
    /// Boolean facts the Breacher can reason about while planning.
    /// Keep this enum below 64 entries because WorldState stores it as bits.
    /// </summary>
    public enum WorldFact
    {
        AtDoor,
        DoorOpen,
        PathToPlayerOpen,
        AtBarricade,
        BarricadeCleared,
        AtPlayer,
        PlayerInRange,
        PlayerDefeated,
        LowHealth,
        InCover
    }

    /// <summary>
    /// An immutable, compact collection of boolean world facts.
    /// </summary>
    public readonly struct WorldState : IEquatable<WorldState>
    {
        private readonly ulong facts;

        private WorldState(ulong facts)
        {
            this.facts = facts;
        }

        public static WorldState Empty => new WorldState(0UL);

        public static WorldState From(params WorldFact[] initialFacts)
        {
            WorldState state = Empty;

            if (initialFacts == null)
            {
                return state;
            }

            foreach (WorldFact fact in initialFacts)
            {
                state = state.With(fact);
            }

            return state;
        }

        public bool IsSet(WorldFact fact)
        {
            return (facts & Mask(fact)) != 0UL;
        }

        public WorldState With(WorldFact fact)
        {
            return new WorldState(facts | Mask(fact));
        }

        public WorldState Without(WorldFact fact)
        {
            return new WorldState(facts & ~Mask(fact));
        }

        public WorldState WithAll(WorldState additions)
        {
            return new WorldState(facts | additions.facts);
        }

        public WorldState WithoutAll(WorldState removals)
        {
            return new WorldState(facts & ~removals.facts);
        }

        public bool Satisfies(WorldState requiredFacts)
        {
            return (facts & requiredFacts.facts) == requiredFacts.facts;
        }

        public int CountMissing(WorldState requiredFacts)
        {
            ulong missing = requiredFacts.facts & ~facts;
            int count = 0;

            while (missing != 0UL)
            {
                missing &= missing - 1UL;
                count++;
            }

            return count;
        }

        public bool Equals(WorldState other)
        {
            return facts == other.facts;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return facts.GetHashCode();
        }

        public static bool operator ==(WorldState left, WorldState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldState left, WorldState right)
        {
            return !left.Equals(right);
        }

        private static ulong Mask(WorldFact fact)
        {
            int index = (int)fact;

            if (index < 0 || index >= 64)
            {
                throw new ArgumentOutOfRangeException(nameof(fact));
            }

            return 1UL << index;
        }
    }
}
