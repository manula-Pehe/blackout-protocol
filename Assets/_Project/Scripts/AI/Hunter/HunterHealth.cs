using BlackoutProtocol.Core;
using BlackoutProtocol.Body;
using UnityEngine;

namespace BlackoutProtocol.AI.Hunter
{
    /// <summary>Allows player weapons to defeat the integrated Hunter cleanly.</summary>
    [DisallowMultipleComponent]
    public sealed class HunterHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        private float currentHealth;

        public bool IsDead => currentHealth <= 0f;

        private void Awake() => currentHealth = Mathf.Max(1f, maximumHealth);

        public void TakeDamage(float amount, Vector3 hitPoint, GameObject source)
        {
            if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            AgentBody body = GetComponent<AgentBody>();
            if (body != null) body.PlayAction(IsDead ? AgentAction.Die : AgentAction.Hit);
            if (!IsDead) return;

            HunterBrain brain = GetComponent<HunterBrain>();
            if (brain != null) brain.enabled = false;
            if (body != null) body.Stop();
        }
    }
}
