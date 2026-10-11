using System;
using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// The movement and animation side of an agent. Every enemy brain controls
    /// its character only through this interface, which keeps AI logic
    /// separate from movement, rotation and animation.
    /// </summary>
    public interface IAgentBody
    {
        /// <summary>Raised once when the body reaches the end of its current path.</summary>
        event Action Arrived;

        /// <summary>Raised when the body has made no progress for a while and cannot continue. The brain should re-plan.</summary>
        event Action Stuck;

        /// <summary>Current world position of the agent.</summary>
        Vector3 Position { get; }

        /// <summary>Current velocity of the agent in world space.</summary>
        Vector3 Velocity { get; }

        /// <summary>True while the body is following a path.</summary>
        bool IsMoving { get; }

        /// <summary>True after the body has reached the end of its current path.</summary>
        bool HasArrived { get; }

        /// <summary>
        /// Starts following a new command. Replaces any current command smoothly,
        /// without stopping first.
        /// </summary>
        void Execute(MoveCommand command);

        /// <summary>Stops moving and clears the current path.</summary>
        void Stop();

        /// <summary>Plays a one-shot action such as an attack or hit reaction.</summary>
        void PlayAction(AgentAction action);
    }
}
