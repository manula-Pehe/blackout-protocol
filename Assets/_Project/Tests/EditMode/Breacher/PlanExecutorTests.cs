using System.Collections.Generic;
using BlackoutProtocol.AI.Breacher.Logic;
using NUnit.Framework;

namespace BlackoutProtocol.AI.Breacher.Tests
{
    public sealed class PlanExecutorTests
    {
        private sealed class FakeActionRunner : IGoapActionRunner
        {
            private readonly Queue<ActionExecutionStatus> results =
                new Queue<ActionExecutionStatus>();

            public readonly List<string> Begun = new List<string>();
            public readonly List<string> Cancelled = new List<string>();

            public void AddResult(ActionExecutionStatus result)
            {
                results.Enqueue(result);
            }

            public void Begin(GoapAction action)
            {
                Begun.Add(action.Name);
            }

            public ActionExecutionStatus Tick(GoapAction action)
            {
                return results.Count > 0
                    ? results.Dequeue()
                    : ActionExecutionStatus.Running;
            }

            public void Cancel(GoapAction action)
            {
                Cancelled.Add(action.Name);
            }
        }

        [Test]
        public void Executor_CompletesActionsInPlanOrder()
        {
            GoapAction move = new MoveToAction(
                "Door",
                WorldState.Empty,
                WorldState.From(WorldFact.AtDoor),
                WorldState.Empty,
                1f);
            GoapAction open = new OpenDoorAction(1f);
            GoapPlanResult plan = SuccessfulPlan(move, open);

            var runner = new FakeActionRunner();
            runner.AddResult(ActionExecutionStatus.Succeeded);
            runner.AddResult(ActionExecutionStatus.Succeeded);
            var executor = new PlanExecutor(runner);

            executor.StartPlan(plan, WorldState.Empty);
            executor.Tick();
            executor.Tick();

            Assert.That(
                executor.Status,
                Is.EqualTo(PlanExecutionStatus.Completed));
            CollectionAssert.AreEqual(
                new[] { "Move To Door", "Open Door" },
                runner.Begun);
            Assert.That(
                executor.CurrentWorldState.IsSet(WorldFact.DoorOpen),
                Is.True);
        }

        [Test]
        public void Executor_RequestsReplanWhenActionFails()
        {
            GoapAction move = new MoveToAction(
                "Door",
                WorldState.Empty,
                WorldState.From(WorldFact.AtDoor),
                WorldState.Empty,
                1f);
            var runner = new FakeActionRunner();
            runner.AddResult(ActionExecutionStatus.Failed);
            var executor = new PlanExecutor(runner);

            executor.StartPlan(SuccessfulPlan(move), WorldState.Empty);
            executor.Tick();

            Assert.That(
                executor.Status,
                Is.EqualTo(PlanExecutionStatus.NeedsReplan));
            Assert.That(
                executor.ReplanReason,
                Is.EqualTo(ReplanReason.ActionFailed));
            CollectionAssert.AreEqual(
                new[] { "Move To Door" },
                runner.Cancelled);
        }

        [Test]
        public void Executor_RequestsReplanWhenLevelChanges()
        {
            GoapAction move = new MoveToAction(
                "Door",
                WorldState.Empty,
                WorldState.From(WorldFact.AtDoor),
                WorldState.Empty,
                1f);
            var runner = new FakeActionRunner();
            var executor = new PlanExecutor(runner);

            executor.StartPlan(SuccessfulPlan(move), WorldState.Empty);
            executor.NotifyLevelChanged();

            Assert.That(
                executor.Status,
                Is.EqualTo(PlanExecutionStatus.NeedsReplan));
            Assert.That(
                executor.ReplanReason,
                Is.EqualTo(ReplanReason.LevelChanged));
        }

        [Test]
        public void Executor_RequestsReplanWhenPreconditionDisappears()
        {
            GoapAction attack = new AttackAction(1f);
            var runner = new FakeActionRunner();
            var executor = new PlanExecutor(runner);

            executor.StartPlan(
                SuccessfulPlan(attack),
                WorldState.From(WorldFact.PlayerInRange));
            executor.UpdateWorldState(WorldState.Empty);

            Assert.That(
                executor.Status,
                Is.EqualTo(PlanExecutionStatus.NeedsReplan));
            Assert.That(
                executor.ReplanReason,
                Is.EqualTo(ReplanReason.PreconditionsFailed));
        }

        [Test]
        public void Executor_CompletesImmediatelyForEmptySuccessfulPlan()
        {
            var runner = new FakeActionRunner();
            var executor = new PlanExecutor(runner);

            executor.StartPlan(
                new GoapPlanResult(
                    true,
                    new GoapAction[0],
                    0f,
                    0,
                    0d),
                WorldState.Empty);

            Assert.That(
                executor.Status,
                Is.EqualTo(PlanExecutionStatus.Completed));
            Assert.That(runner.Begun, Is.Empty);
        }

        private static GoapPlanResult SuccessfulPlan(
            params GoapAction[] actions)
        {
            return new GoapPlanResult(
                true,
                actions,
                0f,
                0,
                0d);
        }
    }
}
