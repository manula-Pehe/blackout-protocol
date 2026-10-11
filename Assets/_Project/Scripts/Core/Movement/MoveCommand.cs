using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// Instruction from an agent's brain to its body: follow this path, in this style,
    /// optionally while facing a target. Brains create commands; bodies execute them.
    /// </summary>
    public readonly struct MoveCommand
    {
        private static readonly IReadOnlyList<Vector3> EmptyPath = Array.Empty<Vector3>();

        /// <summary>
        /// Creates a move command.
        /// </summary>
        /// <param name="path">World-space waypoints, in order. The body copies them, so the caller may reuse its list.</param>
        /// <param name="style">How to move along the path.</param>
        /// <param name="lookTarget">Optional target to face while moving (used by <see cref="MoveStyle.Strafe"/>).</param>
        public MoveCommand(IReadOnlyList<Vector3> path, MoveStyle style, Transform lookTarget = null)
        {
            Path = path ?? EmptyPath;
            Style = style;
            LookTarget = lookTarget;
        }

        /// <summary>World-space waypoints, in order. Never null.</summary>
        public IReadOnlyList<Vector3> Path { get; }

        /// <summary>How to move along the path.</summary>
        public MoveStyle Style { get; }

        /// <summary>Optional target to face while moving. Null means face the direction of travel.</summary>
        public Transform LookTarget { get; }

        /// <summary>True when the command contains at least one waypoint.</summary>
        public bool HasPath => Path != null && Path.Count > 0;

        /// <summary>Final waypoint, or <see cref="Vector3.zero"/> when there is no path.</summary>
        public Vector3 Destination => HasPath ? Path[Path.Count - 1] : Vector3.zero;

        /// <summary>A command that stops movement and optionally keeps facing a target.</summary>
        public static MoveCommand Hold(Transform lookTarget = null)
        {
            return new MoveCommand(EmptyPath, MoveStyle.Idle, lookTarget);
        }
    }
}
