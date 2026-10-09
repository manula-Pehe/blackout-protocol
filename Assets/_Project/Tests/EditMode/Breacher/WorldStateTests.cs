using BlackoutProtocol.AI.Breacher.Logic;
using NUnit.Framework;

namespace BlackoutProtocol.AI.Breacher.Tests
{
    public sealed class WorldStateTests
    {
        [Test]
        public void EqualStates_HaveEqualHashes()
        {
            WorldState first = WorldState.From(
                WorldFact.AtDoor,
                WorldFact.DoorOpen);
            WorldState second = WorldState.Empty
                .With(WorldFact.DoorOpen)
                .With(WorldFact.AtDoor);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void Apply_AddsAndRemovesExpectedFacts()
        {
            WorldState initial = WorldState.From(
                WorldFact.AtDoor,
                WorldFact.LowHealth);

            var action = new GoapActionDefinition(
                "Open Door",
                WorldState.From(WorldFact.AtDoor),
                WorldState.From(
                    WorldFact.DoorOpen,
                    WorldFact.PathToPlayerOpen),
                WorldState.From(WorldFact.AtDoor),
                2f);

            Assert.That(action.IsApplicable(initial), Is.True);

            WorldState result = action.Apply(initial);

            Assert.That(result.IsSet(WorldFact.AtDoor), Is.False);
            Assert.That(result.IsSet(WorldFact.DoorOpen), Is.True);
            Assert.That(
                result.IsSet(WorldFact.PathToPlayerOpen),
                Is.True);
            Assert.That(result.IsSet(WorldFact.LowHealth), Is.True);
        }

        [Test]
        public void CountMissing_ReturnsUnsatisfiedGoalFactCount()
        {
            WorldState current = WorldState.From(WorldFact.DoorOpen);
            WorldState goal = WorldState.From(
                WorldFact.DoorOpen,
                WorldFact.AtPlayer,
                WorldFact.PlayerInRange);

            Assert.That(current.CountMissing(goal), Is.EqualTo(2));
        }
    }
}
