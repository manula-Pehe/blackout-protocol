using System;
using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackoutProtocol.Player
{
    /// <summary>
    /// Fires a centre-screen hitscan weapon and reports damage and gunshot noise
    /// through the shared gameplay contracts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerWeapon : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;

        [Header("Weapon")]
        [SerializeField, Min(0.01f)] private float damage = 25f;
        [SerializeField, Min(0.01f)] private float range = 100f;
        [Tooltip("Maximum shots per second while the attack input is held.")]
        [SerializeField, Min(0.01f)] private float shotsPerSecond = 5f;
        [SerializeField] private LayerMask hitMask = GameLayers.Shootable;

        [Header("Ammunition")]
        [SerializeField, Min(1)] private int magazineSize = 12;
        [SerializeField, Min(0)] private int initialReserveAmmo = 60;
        [SerializeField, Min(0.01f)] private float reloadDuration = 1.5f;

        [Header("Runtime Ammo (visible in Play Mode)")]
        [SerializeField] private int currentAmmo;
        [SerializeField] private int reserveAmmo;
        [SerializeField] private bool isReloading;

        private float nextShotTime;
        private float reloadCompletesAt;
        private PlayerState playerState;
        private PlayerInput playerInput;
        private InputActionAsset playerActions;
        private InputAction attackAction;
        private InputAction reloadAction;

        /// <summary>Raised after magazine or reserve ammunition changes.</summary>
        public event Action<int, int, int> AmmoChanged;

        /// <summary>Raised when a live round is fired.</summary>
        public event Action ShotFired;

        /// <summary>Raised at the world surface hit by a fired round.</summary>
        public event Action<Vector3, Vector3> ShotHit;

        /// <summary>Raised when the weapon is triggered with an empty magazine.</summary>
        public event Action EmptyShot;

        /// <summary>Raised when a reload begins.</summary>
        public event Action ReloadStarted;

        /// <summary>Raised after a reload has completed.</summary>
        public event Action ReloadCompleted;

        public int MagazineSize => magazineSize;
        public int CurrentAmmo => currentAmmo;
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading => isReloading;

        private void Awake()
        {
            magazineSize = Mathf.Max(1, magazineSize);
            initialReserveAmmo = Mathf.Max(0, initialReserveAmmo);
            reloadDuration = Mathf.Max(0.01f, reloadDuration);
            currentAmmo = magazineSize;
            reserveAmmo = initialReserveAmmo;
            playerState = GetComponent<PlayerState>();
            playerInput = GetComponent<PlayerInput>();
            RefreshAttackAction();
        }

        /// <summary>Called by PlayerInput's Send Messages behavior for the Attack action.</summary>
        public void OnAttack(InputValue value)
        {
            if (value.isPressed && !IsReloading)
            {
                TryFire();
            }
        }

        /// <summary>Called by PlayerInput's Send Messages behavior for the Reload action.</summary>
        public void OnReload(InputValue value)
        {
            if (value.isPressed)
            {
                TryStartReload();
            }
        }

        private void Update()
        {
            RefreshAttackAction();

            if (IsReloading)
            {
                if (Time.time >= reloadCompletesAt)
                {
                    CompleteReload();
                }

                return;
            }

            if (reloadAction != null && reloadAction.WasPressedThisFrame())
            {
                TryStartReload();
            }

            // Read the action's live state instead of relying on Send Messages
            // to deliver a release callback and clear a cached held flag.
            if (attackAction != null && attackAction.IsPressed())
            {
                TryFire();
            }
        }

        private void OnDisable()
        {
            isReloading = false;
            playerState?.SetReloading(false);
        }

        private void RefreshAttackAction()
        {
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }

            InputActionAsset actions = playerInput != null
                ? playerInput.actions
                : null;

            if (actions == playerActions)
            {
                return;
            }

            playerActions = actions;
            InputActionMap playerMap = playerActions != null
                ? playerActions.FindActionMap("Player", false)
                : null;
            attackAction = playerMap?.FindAction("Attack", false);
            reloadAction = playerMap?.FindAction("Reload", false);
        }

        private void TryFire()
        {
            if (IsReloading || playerCamera == null || Time.time < nextShotTime)
            {
                return;
            }

            nextShotTime = Time.time + 1f / shotsPerSecond;

            if (currentAmmo <= 0)
            {
                EmptyShot?.Invoke();
                RaiseSoftNoise();
                return;
            }

            currentAmmo--;
            AmmoChanged?.Invoke(currentAmmo, reserveAmmo, magazineSize);
            ShotFired?.Invoke();

            Ray ray = playerCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    range,
                    hitMask,
                    QueryTriggerInteraction.Ignore))
            {
                ShotHit?.Invoke(hit.point, hit.normal);

                IDamageable damageable =
                    hit.collider.GetComponentInParent<IDamageable>();

                damageable?.TakeDamage(damage, hit.point, gameObject);
            }

            GameEvents.RaiseNoise(new NoiseEvent(
                transform.position,
                NoiseLevels.Gunshot,
                NoiseType.Gunshot,
                Time.time,
                gameObject));
        }

        private void TryStartReload()
        {
            if (IsReloading ||
                currentAmmo >= magazineSize ||
                reserveAmmo <= 0)
            {
                return;
            }

            isReloading = true;
            reloadCompletesAt = Time.time + reloadDuration;
            ReloadStarted?.Invoke();

            if (playerState == null)
            {
                playerState = GetComponent<PlayerState>();
            }

            playerState?.SetReloading(true);
            RaiseSoftNoise();
        }

        private void CompleteReload()
        {
            int ammoToLoad = Mathf.Min(magazineSize - currentAmmo, reserveAmmo);
            currentAmmo += ammoToLoad;
            reserveAmmo -= ammoToLoad;
            isReloading = false;
            playerState?.SetReloading(false);
            AmmoChanged?.Invoke(currentAmmo, reserveAmmo, magazineSize);
            ReloadCompleted?.Invoke();
        }

        private void RaiseSoftNoise()
        {
            GameEvents.RaiseNoise(new NoiseEvent(
                transform.position,
                NoiseLevels.WalkingFootstep,
                NoiseType.Footstep,
                Time.time,
                gameObject));
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0.01f, damage);
            range = Mathf.Max(0.01f, range);
            shotsPerSecond = Mathf.Max(0.01f, shotsPerSecond);
            magazineSize = Mathf.Max(1, magazineSize);
            initialReserveAmmo = Mathf.Max(0, initialReserveAmmo);
            reloadDuration = Mathf.Max(0.01f, reloadDuration);
        }
    }
}
