using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// A door that agents can reason about and open.
    /// </summary>
    public interface IDoor
    {
        /// <summary>True while the door is open.</summary>
        bool IsOpen { get; }

        /// <summary>True while the door is locked and cannot be opened.</summary>
        bool IsLocked { get; }

        /// <summary>World position of the doorway.</summary>
        Vector3 Position { get; }

        /// <summary>Opens the door if it is not locked.</summary>
        void Open();
    }
}
