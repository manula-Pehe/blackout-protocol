using System;
using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// Single place where the active player registers itself, so agents can find
    /// the player without scene searches.
    /// </summary>
    public static class PlayerRegistry
    {
        /// <summary>Raised when a player registers or unregisters. The argument is the new current player, or null.</summary>
        public static event Action<IPlayerState> CurrentChanged;

        /// <summary>The active player, or null when none is registered.</summary>
        public static IPlayerState Current { get; private set; }

        /// <summary>Registers the active player. Call from the player's <c>OnEnable</c>.</summary>
        public static void Register(IPlayerState player)
        {
            Current = player;
            CurrentChanged?.Invoke(Current);
        }

        /// <summary>Unregisters the player if it is the current one. Call from the player's <c>OnDisable</c>.</summary>
        public static void Unregister(IPlayerState player)
        {
            if (!ReferenceEquals(Current, player))
            {
                return;
            }

            Current = null;
            CurrentChanged?.Invoke(null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            Current = null;
            CurrentChanged = null;
        }
    }
}
