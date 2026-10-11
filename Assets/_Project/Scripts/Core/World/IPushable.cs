using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// An object that can be pushed, such as a crate or a barricade.
    /// </summary>
    public interface IPushable
    {
        /// <summary>True if the object can currently be pushed.</summary>
        bool CanPush { get; }

        /// <summary>Current world position of the object.</summary>
        Vector3 Position { get; }

        /// <summary>
        /// Pushes the object.
        /// </summary>
        /// <param name="direction">World-space push direction (normalised by the implementation).</param>
        /// <param name="force">Push strength.</param>
        void Push(Vector3 direction, float force);
    }
}
