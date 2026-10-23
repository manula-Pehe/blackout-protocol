using System;

namespace BlackoutProtocol.GameFlow.Logic
{
    /// <summary>The objective sequence and its mutually exclusive outcomes.</summary>
    public enum GameFlowPhase { ReachCore, ActivateConsole, ExitUnlocked, Won, Lost }

    /// <summary>Scene-independent, one-way objective progression. A new scene owns a new state.</summary>
    public sealed class GameFlowState
    {
        public GameFlowPhase Phase { get; private set; } = GameFlowPhase.ReachCore;
        public bool IsTerminal => Phase == GameFlowPhase.Won || Phase == GameFlowPhase.Lost;
        public bool RestartRequested { get; private set; }
        public event Action<GameFlowPhase> Changed;

        public bool ReachCore() => Phase == GameFlowPhase.ReachCore && Transition(GameFlowPhase.ActivateConsole);
        public bool ActivateConsole() => Phase == GameFlowPhase.ActivateConsole && Transition(GameFlowPhase.ExitUnlocked);

        /// <summary>Death takes precedence over extraction; a previously committed outcome never changes.</summary>
        public bool Extract(bool playerAlive)
        {
            if (!playerAlive) return Die();
            return Phase == GameFlowPhase.ExitUnlocked && Transition(GameFlowPhase.Won);
        }

        public bool Die() => !IsTerminal && Transition(GameFlowPhase.Lost);

        public bool RequestRestart()
        {
            if (!IsTerminal || RestartRequested) return false;
            RestartRequested = true;
            return true;
        }

        /// <summary>Allows another request only when scene loading failed.</summary>
        public void RestartFailed() { RestartRequested = false; }

        private bool Transition(GameFlowPhase phase)
        {
            Phase = phase;
            Changed?.Invoke(phase);
            return true;
        }
    }
}
