using System.Collections.Generic;
using BlackoutProtocol.AI.Flanker.Logic;
using NUnit.Framework;

namespace BlackoutProtocol.AI.Flanker.Tests
{
    public sealed class FlankerBehaviourTests
    {
        private sealed class Actions : IFlankerActions
        {
            public readonly List<string> Events = new List<string>();
            public FlankerTactic Failing;
            public BehaviourStatus Tick(FlankerTactic tactic)
            {
                Events.Add("Tick " + tactic);
                return tactic == Failing ? BehaviourStatus.Failure : BehaviourStatus.Running;
            }
            public void Abort(FlankerTactic tactic) => Events.Add("Abort " + tactic);
        }

        [TestCase(0.29f, true, true, true, true, FlankerTactic.Retreat)]
        [TestCase(0.30f, true, true, true, true, FlankerTactic.Punish)]
        [TestCase(1f, true, false, true, true, FlankerTactic.Hold)]
        [TestCase(1f, true, false, false, true, FlankerTactic.Flank)]
        [TestCase(1f, false, true, true, true, FlankerTactic.Investigate)]
        [TestCase(1f, false, true, true, false, FlankerTactic.Patrol)]
        public void PrioritiesUsePerceivedFacts(float ownHealth, bool visible, bool reloading, bool aimed, bool memory, FlankerTactic expected)
        {
            var tree = new FlankerBehaviourTree();
            tree.Tick(new FlankerFacts { OwnHealth = ownHealth, PlayerHealth = 1f, PlayerVisible = visible,
                PlayerReloading = reloading, AimedAt = aimed, HasMemory = memory }, new Actions());
            Assert.That(tree.Active, Is.EqualTo(expected));
        }

        [TestCase(0.349f, FlankerTactic.Punish)]
        [TestCase(0.35f, FlankerTactic.Hold)]
        public void PlayerHealthThresholdIsStrict(float playerHealth, FlankerTactic expected)
        {
            var tree = new FlankerBehaviourTree();
            tree.Tick(new FlankerFacts { OwnHealth = 1f, PlayerVisible = true, PlayerHealth = playerHealth, AimedAt = true }, new Actions());
            Assert.That(tree.Active, Is.EqualTo(expected));
        }

        [Test]
        public void HigherPriorityAbortsBeforeNewActionAndUnchangedActionIsNotAborted()
        {
            var tree = new FlankerBehaviourTree();
            var actions = new Actions();
            var facts = new FlankerFacts { OwnHealth = 1f, PlayerVisible = true, PlayerHealth = 1f };
            tree.Tick(facts, actions);
            tree.Tick(facts, actions);
            facts.OwnHealth = 0.2f;
            tree.Tick(facts, actions);
            CollectionAssert.AreEqual(new[] { "Tick Flank", "Tick Flank", "Abort Flank", "Tick Retreat" }, actions.Events);
        }

        [Test]
        public void FailedFlankReleasesBeforeInvestigating()
        {
            var tree = new FlankerBehaviourTree();
            var actions = new Actions { Failing = FlankerTactic.Flank };
            tree.Tick(new FlankerFacts { OwnHealth = 1f, PlayerHealth = 1f, PlayerVisible = true, HasMemory = true }, actions);
            CollectionAssert.AreEqual(new[] { "Tick Flank", "Abort Flank", "Tick Investigate" }, actions.Events);
            Assert.That(tree.Active, Is.EqualTo(FlankerTactic.Investigate));
        }

        [Test]
        public void DeathAbortsOnceAndNeverExecutesActions()
        {
            var tree = new FlankerBehaviourTree();
            var actions = new Actions();
            tree.Tick(new FlankerFacts { OwnHealth = 1f }, actions);
            actions.Events.Clear();
            Assert.That(tree.Tick(new FlankerFacts { OwnHealth = 0f }, actions), Is.EqualTo(BehaviourStatus.Failure));
            tree.Tick(new FlankerFacts { OwnHealth = 0f }, actions);
            CollectionAssert.AreEqual(new[] { "Abort Patrol" }, actions.Events);
            Assert.That(tree.Active, Is.EqualTo(FlankerTactic.None));
        }

        [Test]
        public void LostEvidenceFallsBackToPatrol()
        {
            var tree = new FlankerBehaviourTree();
            var actions = new Actions();
            tree.Tick(new FlankerFacts { OwnHealth = 1f, HasMemory = true }, actions);
            tree.Tick(new FlankerFacts { OwnHealth = 1f }, actions);
            Assert.That(tree.Active, Is.EqualTo(FlankerTactic.Patrol));
            Assert.That(actions.Events, Does.Contain("Abort Investigate"));
        }

        [Test]
        public void MemoryExpiresWithoutTrackingTheLivePlayer()
        {
            var memory = new FlankerMemory();
            memory.Remember(new FlankerPoint(2f, 0f, 3f), 10d, 8d);
            Assert.That(memory.HasEvidence(17.99d), Is.True);
            Assert.That(memory.HasEvidence(18d), Is.False);
            Assert.That(memory.Position.X, Is.EqualTo(2f));
            memory.Remember(new FlankerPoint(4f, 0f, 5f), 20d, 8d);
            memory.Clear();
            Assert.That(memory.HasEvidence(21d), Is.False);
        }

        [Test]
        public void HealthIgnoresInvalidDamageAndCannotResurrect()
        {
            var health = new FlankerVitals(100f);
            foreach (float amount in new[] { -1f, 0f, float.NaN, float.PositiveInfinity }) Assert.That(health.Damage(amount), Is.False);
            health.Damage(71f);
            Assert.That(health.Normalized, Is.EqualTo(0.29f));
            health.Damage(1000f);
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.Current, Is.Zero);
            Assert.That(health.Damage(10f), Is.False);
        }

        [Test]
        public void CoverRejectsOccupiedUnreachableAndFrontalCandidates()
        {
            Assert.That(FlankerCoverScoring.Score(true, false, 1f, 2d, 0f, 8f, false), Is.EqualTo(double.NegativeInfinity));
            Assert.That(FlankerCoverScoring.Score(false, true, 1f, 2d, 0f, 8f, false), Is.EqualTo(double.NegativeInfinity));
            Assert.That(FlankerCoverScoring.Score(true, true, 1f, 2d, 0.8f, 8f, false), Is.EqualTo(double.NegativeInfinity));
            Assert.That(FlankerCoverScoring.Score(true, true, 0f, 2d, 0f, 8f, false), Is.EqualTo(double.NegativeInfinity));
        }

        [Test]
        public void CoverPrefersRearAnglesAndRetreatDistance()
        {
            Assert.That(FlankerCoverScoring.Score(true, true, 1f, 2d, -1f, 8f, false),
                Is.GreaterThan(FlankerCoverScoring.Score(true, true, 1f, 2d, 0f, 8f, false)));
            Assert.That(FlankerCoverScoring.Score(true, true, 1f, 2d, 0f, 15f, true),
                Is.GreaterThan(FlankerCoverScoring.Score(true, true, 1f, 2d, 0f, 8f, true)));
        }

        [TestCase(true, true, 0f, true, 10d, 0d, true)]
        [TestCase(true, true, 5f, false, 10d, 0d, true)]
        [TestCase(true, true, 5f, true, 10d, 11d, true)]
        [TestCase(true, true, 5f, true, 10d, 9d, false)]
        [TestCase(true, false, 0f, true, 10d, 0d, false)]
        [TestCase(false, true, 0f, true, 10d, 0d, false)]
        public void RouteReplacementWaitsForACornerAndMeaningfulGoalChange(bool commanded, bool moving, float change,
            bool corner, double now, double earliest, bool keep)
        {
            Assert.That(FlankerRoutePolicy.KeepCurrent(commanded, moving, change, corner, now, earliest), Is.EqualTo(keep));
        }
    }
}
