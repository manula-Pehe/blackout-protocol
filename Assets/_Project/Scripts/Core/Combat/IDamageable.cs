using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// Anything that can take damage: the player and every enemy.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>True once health has reached zero.</summary>
        bool IsDead { get; }

        /// <summary>
        /// Applies damage.
        /// </summary>
        /// <param name="amount">Damage to apply. Must be positive.</param>
        /// <param name="hitPoint">World-space point where the hit landed.</param>
        /// <param name="source">The object that caused the damage (may be null).</param>
        void TakeDamage(float amount, Vector3 hitPoint, GameObject source);
    }
}
