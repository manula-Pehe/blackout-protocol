using System.Collections.Generic;
using BlackoutProtocol.AI.Breacher.Logic;
using NUnit.Framework;

namespace BlackoutProtocol.AI.Breacher.Tests
{
    public sealed class GoapPlannerTests
    {
        [Test]
        public void Planner_FindsDoorToAttackSequence()
        {
            GoapAction moveToDoor = Action(
                "Move To Door",
                WorldState.Empty,
                WorldState.From(WorldFact.AtDoor),
                WorldState.Empty,
                1f);
            GoapAction openDoor = Action(
                "Open Door",
                WorldState.From(WorldFact.AtDoor),
                WorldState.From(
                    WorldFact.DoorOpen,
                    WorldFact.PathToPlayerOpen),
                WorldState.From(WorldFact.AtDoor),
                2f);
            GoapAction moveToPlayer = Action(
                "Move To Player",
                WorldState.From(WorldFact.PathToPlayerOpen),
                WorldState.From(
                    WorldFact.AtPlayer,
                    WorldFact.PlayerInRange),
                WorldState.Empty,
                3f);
            GoapAction attack = Action(
                "Attack",
                WorldState.From(WorldFact.PlayerInRange),
                WorldState.From(WorldFact.PlayerDefeated),
                WorldState.Empty,
                1f);

            GoapPlanResult result = GoapPlanner.Plan(
                WorldState.Empty,
                WorldState.From(WorldFact.PlayerDefeated),
                new[] { attack, moveToPlayer, openDoor, moveToDoor });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.TotalCost, Is.EqualTo(7f).Within(0.001f));
            CollectionAssert.AreEqual(
                new[]
                {
                    "Move To Door",
                    "Open Door",
                    "Move To Player",
                    "Attack"
                },
                Names(result.Actions));
        }

        [Test]
        public void Planner_ChoosesCheaperValidPlan()
        {
            GoapAction longDetour = Action(
                "Take Long Detour",
                WorldState.Empty,
                WorldState.From(WorldFact.PathToPlayerOpen),
                WorldState.Empty,
                8f);
            GoapAction pushBarricade = Action(
                "Push Barricade",
                WorldState.Empty,
                WorldState.From(WorldFact.PathToPlayerOpen),
                WorldState.Empty,
                3f);

            GoapPlanResult result = GoapPlanner.Plan(
                WorldState.Empty,
                WorldState.From(WorldFact.PathToPlayerOpen),
                new[] { longDetour, pushBarricade });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.TotalCost, Is.EqualTo(3f));
            Assert.That(result.Actions[0].Name, Is.EqualTo("Push Barricade"));
        }

        [Test]
        public void Planner_ReturnsFailureWhenGoalIsUnreachable()
        {
            GoapAction takeCover = Action(
                "Take Cover",
                WorldState.Empty,
                WorldState.From(WorldFact.InCover),
                WorldState.Empty,
                1f);

            GoapPlanResult result = GoapPlanner.Plan(
                WorldState.Empty,
                WorldState.From(WorldFact.PlayerDefeated),
                new[] { takeCover });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Actions, Is.Empty);
            Assert.That(float.IsPositiveInfinity(result.TotalCost), Is.True);
        }

        [Test]
        public void Planner_StopsAtExpansionLimit()
        {
            GoapAction moveToDoor = Action(
                "Move To Door",
                WorldState.Empty,
                WorldState.From(WorldFact.AtDoor),
                WorldState.Empty,
                1f);
            GoapAction openDoor = Action(
                "Open Door",
                WorldState.From(WorldFact.AtDoor),
                WorldState.From(WorldFact.DoorOpen),
                WorldState.Empty,
                1f);

            GoapPlanResult result = GoapPlanner.Plan(
                WorldState.Empty,
                WorldState.From(WorldFact.DoorOpen),
                new[] { moveToDoor, openDoor },
                maximumExpandedStates: 1);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExpandedStateCount, Is.EqualTo(1));
        }

        private static GoapAction Action(
            string name,
            WorldState preconditions,
            WorldState additions,
            WorldState removals,
            float cost)
        {
            return new GoapActionDefinition(
                name,
                preconditions,
                additions,
                removals,
                cost);
        }

        private static IReadOnlyList<string> Names(
            IReadOnlyList<GoapAction> actions)
        {
            var names = new List<string>(actions.Count);

            foreach (GoapAction action in actions)
            {
                names.Add(action.Name);
            }

            return names;
        }
    }
}
