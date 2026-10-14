using System;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Player
{
    /// <summary>
    /// Owns the player's health and implements the shared damage contract.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        private float currentHealth;
        private bool initialized;

        /// <summary>Raised after health changes, with the current and maximum values.</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>Raised once when health reaches zero. The argument is the damage source.</summary>
        public event Action<GameObject> Died;

        public float MaxHealth
        {
            get
            {
                EnsureInitialized();
                return maxHealth;
            }
        }

        public float CurrentHealth
        {
            get
            {
                EnsureInitialized();
                return currentHealth;
            }
        }

        public float HealthNormalized
        {
            get
            {
                EnsureInitialized();
                return currentHealth / maxHealth;
            }
        }

        public bool IsDead
        {
            get
            {
                EnsureInitialized();
                return currentHealth <= 0f;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        public void TakeDamage(float amount, Vector3 hitPoint, GameObject source)
        {
            EnsureInitialized();

            if (IsDead || amount <= 0f || float.IsNaN(amount))
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            HealthChanged?.Invoke(currentHealth, maxHealth);

            if (IsDead)
            {
                Died?.Invoke(source);
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = maxHealth;
            initialized = true;
        }
    }
}
