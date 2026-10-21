using System;

namespace BlackoutProtocol.AI.Sentinel
{
    /// <summary>The leaf states used by the Sentinel's operational state.</summary>
    public enum SentinelState
    {
        Patrol,
        Alert,
        Track,
        Search,
        Return
    }

    /// <summary>Composite parent states grouping the Sentinel's leaf states.</summary>
    public enum SentinelStateGroup
    {
        Operational,
        Routine,
        Contact
    }

    /// <summary>Inputs that drive one HSM transition step.</summary>
    public readonly struct SentinelHsmInput
    {
        public SentinelHsmInput(bool playerVisible, bool alertElapsed, bool searchComplete, bool returnComplete)
        {
            PlayerVisible = playerVisible;
            AlertElapsed = alertElapsed;
            SearchComplete = searchComplete;
            ReturnComplete = returnComplete;
        }

        public bool PlayerVisible { get; }
        public bool AlertElapsed { get; }
        public bool SearchComplete { get; }
        public bool ReturnComplete { get; }
    }

    /// <summary>
    /// Pure decision layer for the Sentinel's operational HSM. The operational
    /// parent contains Patrol/Return routine states and Alert/Track/Search contact
    /// states; this class decides transitions while the Unity adapter performs I/O.
    /// </summary>
    public sealed class SentinelHsm
    {
        /// <summary>Raised when the active leaf state changes.</summary>
        public event Action<SentinelState, SentinelState> StateChanged;
        /// <summary>Raised immediately before the active leaf state is exited.</summary>
        public event Action<SentinelState> StateExiting;
        /// <summary>Raised after a new leaf state has been entered.</summary>
        public event Action<SentinelState> StateEntered;

        public SentinelState State { get; private set; } = SentinelState.Patrol;
        /// <summary>The top-level operational composite that owns the routine and contact groups.</summary>
        public SentinelStateGroup RootState => SentinelStateGroup.Operational;
        /// <summary>The active composite parent for the current leaf state.</summary>
        public SentinelStateGroup ParentState => State == SentinelState.Patrol || State == SentinelState.Return
            ? SentinelStateGroup.Routine
            : SentinelStateGroup.Contact;
        public float StateElapsed { get; private set; }

        /// <summary>Advances the current state and applies at most one transition.</summary>
        public void Tick(SentinelHsmInput input, float deltaTime)
        {
            StateElapsed += Math.Max(0f, deltaTime);
            SentinelState? next = null;

            switch (State)
            {
                case SentinelState.Patrol:
                    if (input.PlayerVisible) next = SentinelState.Alert;
                    break;
                case SentinelState.Alert:
                    if (input.AlertElapsed)
                    {
                        next = input.PlayerVisible ? SentinelState.Track : SentinelState.Search;
                    }
                    break;
                case SentinelState.Track:
                    if (!input.PlayerVisible) next = SentinelState.Search;
                    break;
                case SentinelState.Search:
                    if (input.PlayerVisible) next = SentinelState.Alert;
                    else if (input.SearchComplete) next = SentinelState.Return;
                    break;
                case SentinelState.Return:
                    if (input.PlayerVisible) next = SentinelState.Alert;
                    else if (input.ReturnComplete) next = SentinelState.Patrol;
                    break;
            }

            if (next.HasValue)
            {
                TransitionTo(next.Value);
            }
        }

        /// <summary>Resets the HSM to its initial patrol state.</summary>
        public void Reset()
        {
            SentinelState previous = State;
            if (previous != State)
            {
                StateExiting?.Invoke(previous);
            }
            State = SentinelState.Patrol;
            StateElapsed = 0f;
            if (previous != State)
            {
                StateEntered?.Invoke(State);
                StateChanged?.Invoke(previous, State);
            }
        }

        private void TransitionTo(SentinelState next)
        {
            if (State == next)
            {
                return;
            }

            SentinelState previous = State;
            StateExiting?.Invoke(previous);
            State = next;
            StateElapsed = 0f;
            StateEntered?.Invoke(next);
            StateChanged?.Invoke(previous, next);
        }
    }
}
