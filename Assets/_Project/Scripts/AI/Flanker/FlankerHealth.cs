using System;
using BlackoutProtocol.AI.Flanker.Logic;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.AI.Flanker
{
    /// <summary>Flanker-local damage adapter; does not change shared enemy-health contracts.</summary>
    [DisallowMultipleComponent]
    public sealed class FlankerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        private FlankerVitals vitals;

        public event Action Damaged;
        public event Action Died;
        public float HealthNormalized { get { EnsureInitialized(); return vitals.Normalized; } }
        public bool IsDead { get { EnsureInitialized(); return vitals.IsDead; } }

        private void Awake() => EnsureInitialized();

        public void TakeDamage(float amount, Vector3 hitPoint, GameObject source)
        {
            EnsureInitialized();
            if (!vitals.Damage(amount)) return;
            if (vitals.IsDead) Died?.Invoke();
            else Damaged?.Invoke();
        }

        private void EnsureInitialized()
        {
            if (vitals == null) vitals = new FlankerVitals(FlankerPoint.Finite(maximumHealth) ? Mathf.Max(1f, maximumHealth) : 100f);
        }

        [ContextMenu("Flanker Debug/Apply 80 Damage (Retreat)")]
        private void DebugRetreat()
        {
            if (Application.isPlaying) TakeDamage(80f, transform.position, null);
        }

        [ContextMenu("Flanker Debug/Kill")]
        private void DebugKill()
        {
            if (Application.isPlaying) TakeDamage(float.MaxValue, transform.position, null);
        }
    }
}
