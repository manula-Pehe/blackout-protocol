using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// Read-only view of the player that agents can sense and react to.
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>The player's transform.</summary>
        Transform Transform { get; }

        /// <summary>Current world position of the player.</summary>
        Vector3 Position { get; }

        /// <summary>Current velocity of the player in world space.</summary>
        Vector3 Velocity { get; }

        /// <summary>Current health from 0 (dead) to 1 (full).</summary>
        float HealthNormalized { get; }

        /// <summary>True while the player is alive.</summary>
        bool IsAlive { get; }

        /// <summary>True while the weapon is reloading.</summary>
        bool IsReloading { get; }

        /// <summary>True while the player is crouching.</summary>
        bool IsCrouching { get; }

        /// <summary>True while the player is running.</summary>
        bool IsRunning { get; }

        /// <summary>The object currently under the player's crosshair, or null.</summary>
        Transform AimTarget { get; }
    }
}
