namespace BlackoutProtocol.Core
{
    /// <summary>
    /// One-shot actions a brain can ask its body to perform.
    /// The body plays the matching animation and timing.
    /// </summary>
    public enum AgentAction
    {
        /// <summary>Perform the agent's attack.</summary>
        Attack,

        /// <summary>React to being hit.</summary>
        Hit,

        /// <summary>Play the death sequence and stop all movement.</summary>
        Die,

        /// <summary>Signal that the agent has noticed something (e.g. a head turn or alert pose).</summary>
        Alert
    }
}
