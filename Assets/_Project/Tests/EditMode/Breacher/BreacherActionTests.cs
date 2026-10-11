using BlackoutProtocol.AI.Breacher.Logic;
using NUnit.Framework;

namespace BlackoutProtocol.AI.Breacher.Tests
{
    public sealed class BreacherActionTests
    {
        [Test]
        public void ConcreteActions_CreateDoorToAttackPlan()
        {
            WorldState locations = WorldState.From(
                WorldFact.AtDoor,
                WorldFact.AtPlayer,
                WorldFact.PlayerInRange);

            var moveToDoor = new MoveToAction(
                "Door",
                WorldState.Empty,
                WorldState.From(WorldFact.AtDoor),
                locations,
                1f);
            var openDoor = new OpenDoorAction(2f);
            var moveToPlayer = new MoveToAction(
                "Player",
                WorldState.From(WorldFact.PathToPlayerOpen),
                WorldState.From(
                    WorldFact.AtPlayer,
                    WorldFact.PlayerInRange),
                locations,
                3f);
            var attack = new AttackAction(1f);

            GoapPlanResult plan = GoapPlanner.Plan(
                WorldState.Empty,
                WorldState.From(WorldFact.PlayerDefeated),
                new GoapAction[]
                {
                    attack,
                    moveToPlayer,
                    openDoor,
                    moveToDoor
                });

            Assert.That(plan.Succeeded, Is.True);
            Assert.That(plan.Actions.Count, Is.EqualTo(4));
            Assert.That(plan.Actions[0].Name, Is.EqualTo("Move To Door"));
            Assert.That(plan.Actions[1].Name, Is.EqualTo("Open Door"));
            Assert.That(plan.Actions[2].Name, Is.EqualTo("Move To Player"));
            Assert.That(plan.Actions[3].Name, Is.EqualTo("Attack"));
        }

        [Test]
        public void TakeCover_IsOnlyApplicableAtLowHealth()
        {
            var takeCover = new TakeCoverAction(2f);

            Assert.That(
                takeCover.IsApplicable(WorldState.Empty),
                Is.False);
            Assert.That(
                takeCover.IsApplicable(
                    WorldState.From(WorldFact.LowHealth)),
                Is.True);
        }
    }
}
