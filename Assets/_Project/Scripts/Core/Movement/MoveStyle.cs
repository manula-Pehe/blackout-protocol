namespace BlackoutProtocol.Core
{
    /// <summary>
    /// How an agent should move along a path. The agent body maps each style
    /// to a speed and an animation set.
    /// </summary>
    public enum MoveStyle
    {
        /// <summary>Stand still (used with an empty path, e.g. to only turn towards a target).</summary>
        Idle,

        /// <summary>Normal patrol pace.</summary>
        Walk,

        /// <summary>Fast movement, e.g. chasing.</summary>
        Run,

        /// <summary>Slow, quiet movement, e.g. searching.</summary>
        Sneak,

        /// <summary>Move along the path while facing <see cref="MoveCommand.LookTarget"/>.</summary>
        Strafe
    }
}
