using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.AI.Hunter.Testing
{
    /// <summary>Stationary, damageable registered player stand-in, used only by the Hunter sandbox.</summary>
    public sealed class HunterSandboxTarget : MonoBehaviour, IPlayerState, IDamageable
    {
        [SerializeField, Min(1)] private float maximumHealth = 100f;
        private float health;
        public Transform Transform => transform;
        public Vector3 Position => transform.position;
        public Vector3 Velocity => Vector3.zero;
        public float HealthNormalized => Mathf.Clamp01(health / Mathf.Max(1, maximumHealth));
        public bool IsAlive => isActiveAndEnabled && health > 0;
        public bool IsDead => !IsAlive;
        public bool IsReloading => false;
        public bool IsCrouching => false;
        public bool IsRunning => false;
        public Transform AimTarget => null;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            health = maximumHealth;
            PlayerRegistry.Register(this);
        }

        private void OnDisable()
        {
            if (Application.isPlaying) PlayerRegistry.Unregister(this);
        }

        public void TakeDamage(float amount, Vector3 hitPoint, GameObject source)
        {
            if (!IsAlive || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return;
            health = Mathf.Max(0, health - amount);
            if (health == 0) PlayerRegistry.Unregister(this);
        }
    }
}
