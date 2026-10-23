using System.Collections.Generic;
using BlackoutProtocol.GameFlow.Logic;
using NUnit.Framework;

namespace BlackoutProtocol.GameFlow.Tests
{
    /// <summary>Protects progression, terminal outcomes and retry behavior without loading a Unity scene.</summary>
    public sealed class GameFlowStateTests
    {
        [Test]
        public void CompleteSequenceWinsOnlyAfterConsole()
        {
            var state = new GameFlowState();
            var changes = new List<GameFlowPhase>();
            state.Changed += changes.Add;
            Assert.That(state.Phase, Is.EqualTo(GameFlowPhase.ReachCore));
            Assert.That(state.Extract(true), Is.False);
            Assert.That(state.ActivateConsole(), Is.False);
            Assert.That(state.ReachCore(), Is.True);
            Assert.That(state.Extract(true), Is.False);
            Assert.That(state.ActivateConsole(), Is.True);
            Assert.That(state.Extract(true), Is.True);
            Assert.That(changes, Is.EqualTo(new[] { GameFlowPhase.ActivateConsole, GameFlowPhase.ExitUnlocked, GameFlowPhase.Won }));
        }

        [TestCase(GameFlowPhase.ReachCore)]
        [TestCase(GameFlowPhase.ActivateConsole)]
        [TestCase(GameFlowPhase.ExitUnlocked)]
        public void DeathLosesAtEveryNonterminalPhase(GameFlowPhase phase)
        {
            GameFlowState state = At(phase);
            Assert.That(state.Die(), Is.True);
            Assert.That(state.Phase, Is.EqualTo(GameFlowPhase.Lost));
            Assert.That(state.Extract(true), Is.False);
            Assert.That(state.Die(), Is.False);
        }

        [Test]
        public void DeadPlayerCannotExtractEvenAfterUnlock()
        {
            GameFlowState state = At(GameFlowPhase.ExitUnlocked);
            state.Extract(false);
            Assert.That(state.Phase, Is.EqualTo(GameFlowPhase.Lost));
        }

        [Test]
        public void DuplicateActivationDoesNotRepeatNotifications()
        {
            GameFlowState state = At(GameFlowPhase.ActivateConsole);
            int notifications = 0;
            state.Changed += phase => notifications++;
            Assert.That(state.ReachCore(), Is.False);
            Assert.That(state.ActivateConsole(), Is.True);
            Assert.That(state.ActivateConsole(), Is.False);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TerminalOutcomeCannotBeChanged(bool win)
        {
            GameFlowState state = At(GameFlowPhase.ExitUnlocked);
            if (win) state.Extract(true); else state.Die();
            GameFlowPhase outcome = state.Phase;
            Assert.That(state.ReachCore(), Is.False);
            Assert.That(state.ActivateConsole(), Is.False);
            Assert.That(state.Die(), Is.False);
            Assert.That(state.Extract(true), Is.False);
            Assert.That(state.Phase, Is.EqualTo(outcome));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RestartIsTerminalOnlyAndDeduplicatedWithFailureRetry(bool win)
        {
            var state = new GameFlowState();
            Assert.That(state.RequestRestart(), Is.False);
            state.ReachCore();
            state.ActivateConsole();
            if (win) state.Extract(true); else state.Die();
            Assert.That(state.RequestRestart(), Is.True);
            Assert.That(state.RequestRestart(), Is.False);
            state.RestartFailed();
            Assert.That(state.RequestRestart(), Is.True);
            var reloaded = new GameFlowState();
            Assert.That(reloaded.Phase, Is.EqualTo(GameFlowPhase.ReachCore));
            Assert.That(reloaded.RestartRequested, Is.False);
        }

        private static GameFlowState At(GameFlowPhase phase)
        {
            var state = new GameFlowState();
            if (phase != GameFlowPhase.ReachCore) state.ReachCore();
            if (phase == GameFlowPhase.ExitUnlocked) state.ActivateConsole();
            return state;
        }
    }
}
