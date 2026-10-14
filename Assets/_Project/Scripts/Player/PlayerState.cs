using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Player
{
    /// <summary>
    /// Publishes the player's current state through the shared AI-facing contract.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth), typeof(FirstPersonController))]
    public sealed class PlayerState : MonoBehaviour, IPlayerState
    {
        [Header("Aim")]
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0.01f)] private float aimRange = 100f;
        [SerializeField] private LayerMask aimMask = GameLayers.Shootable;

        private PlayerHealth playerHealth;
        private FirstPersonController firstPersonController;
        private Transform aimTarget;

        public Transform Transform => transform;
        public Vector3 Position => transform.position;
        public Vector3 Velocity => firstPersonController != null
            ? firstPersonController.Velocity
            : Vector3.zero;
        public float HealthNormalized => playerHealth != null
            ? playerHealth.HealthNormalized
            : 0f;
        public bool IsAlive => playerHealth != null && !playerHealth.IsDead;
        public bool IsReloading { get; private set; }
        public bool IsCrouching => firstPersonController != null && firstPersonController.IsCrouching;
        public bool IsRunning => firstPersonController != null && firstPersonController.IsRunning;
        public Transform AimTarget => aimTarget;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            PlayerRegistry.Register(this);
        }

        private void OnDisable()
        {
            PlayerRegistry.Unregister(this);
        }

        private void Update()
        {
            UpdateAimTarget();
        }

        /// <summary>Allows the weapon/reload system to publish its reload state.</summary>
        public void SetReloading(bool isReloading)
        {
            IsReloading = isReloading;
        }

        private void CacheReferences()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (firstPersonController == null)
            {
                firstPersonController = GetComponent<FirstPersonController>();
            }

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }
        }

        private void UpdateAimTarget()
        {
            if (playerCamera == null ||
                !Physics.Raycast(
                    playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)),
                    out RaycastHit hit,
                    aimRange,
                    aimMask,
                    QueryTriggerInteraction.Ignore))
            {
                aimTarget = null;
                return;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            aimTarget = damageable is Component damageableComponent
                ? damageableComponent.transform
                : hit.collider.transform;
        }

        private void OnValidate()
        {
            aimRange = Mathf.Max(0.01f, aimRange);
        }
    }
}
