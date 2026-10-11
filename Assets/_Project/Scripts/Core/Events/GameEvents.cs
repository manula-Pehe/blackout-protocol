using System;
using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// Game-wide events that connect the player, the level and the agents
    /// without direct references between them.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Raised whenever a sound is made.</summary>
        public static event Action<NoiseEvent> Noise;

        /// <summary>
        /// Raised when the walkable layout changes, such as a door opening or closing,
        /// or a barricade or crate coming to rest in a new place. The argument is the
        /// collider of the object that changed.
        /// </summary>
        public static event Action<Collider> LevelChanged;

        /// <summary>Raised when an alarm is triggered. The argument is the player's last seen position.</summary>
        public static event Action<Vector3> AlarmRaised;

        /// <summary>Broadcasts a sound to all listeners.</summary>
        public static void RaiseNoise(NoiseEvent noise)
        {
            Noise?.Invoke(noise);
        }

        /// <summary>Broadcasts that the walkable layout has changed.</summary>
        public static void RaiseLevelChanged(Collider changedObject)
        {
            LevelChanged?.Invoke(changedObject);
        }

        /// <summary>Broadcasts an alarm with the player's last seen position.</summary>
        public static void RaiseAlarm(Vector3 playerPosition)
        {
            AlarmRaised?.Invoke(playerPosition);
        }

        /// <summary>
        /// Clears all subscribers when entering Play Mode, so listeners from a previous
        /// session never leak when domain reload is disabled.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            Noise = null;
            LevelChanged = null;
            AlarmRaised = null;
        }
    }
}
