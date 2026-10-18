using System;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.AI.Breacher
{
    /// <summary>
    /// Damage state used by the Breacher's Recover In Cover GOAP goal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreacherHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Range(0.05f, 0.95f)]
        private float lowHealthThreshold = 0.35f;

        private float currentHealth;
        private bool initialized;

        public event Action StateChanged;

        public float CurrentHealth
        {
            get
            {
                EnsureInitialized();
                return currentHealth;
            }
        }

        public float MaximumHealth => maximumHealth;
        public bool IsLowHealth => !IsDead &&
            CurrentHealth / maximumHealth <= lowHealthThreshold;
        public bool IsDead => CurrentHealth <= 0f;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void TakeDamage(
            float amount,
            Vector3 hitPoint,
            GameObject source)
        {
            EnsureInitialized();

            if (IsDead || amount <= 0f || float.IsNaN(amount))
            {
                return;
            }

            bool wasLowHealth = IsLowHealth;
            currentHealth = Mathf.Max(0f, currentHealth - amount);

            Debug.Log(
                $"Breacher health: {currentHealth:F0}/{maximumHealth:F0}",
                this);

            if (IsDead || wasLowHealth != IsLowHealth)
            {
                StateChanged?.Invoke();
            }
        }

        [ContextMenu("Debug/Apply 75 Damage")]
        private void ApplyDebugDamage()
        {
            TakeDamage(75f, transform.position, gameObject);
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            maximumHealth = Mathf.Max(1f, maximumHealth);
            currentHealth = maximumHealth;
            initialized = true;
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(1f, maximumHealth);
            lowHealthThreshold = Mathf.Clamp(
                lowHealthThreshold,
                0.05f,
                0.95f);
        }
    }
}
