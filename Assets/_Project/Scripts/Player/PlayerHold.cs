using BlackoutProtocol.Core;
using BlackoutProtocol.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackoutProtocol.Player
{
    /// <summary>
    /// Holds one throwable at a camera-relative point and releases it with an impulse.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHold : MonoBehaviour
    {
        [SerializeField] private Transform holdPoint;
        [SerializeField, Min(0.1f)] private float throwImpulse = 12f;

        private Throwable heldObject;
        private Collider[] playerColliders;
        private Camera playerCamera;
        private PlayerInput playerInput;
        private InputActionAsset playerActions;
        private InputAction throwAction;
        private InputAction attackAction;
        private readonly RaycastHit[] obstructionHits = new RaycastHit[24];

        public bool IsHoldingObject => heldObject != null;
        public int LastThrowFrame { get; private set; } = -1;

        private void Awake()
        {
            playerColliders = GetComponentsInChildren<Collider>();
            playerCamera = GetComponentInChildren<Camera>();
            playerInput = GetComponent<PlayerInput>();
            RefreshActions();
        }

        private void Update()
        {
            RefreshActions();
            if (heldObject != null &&
                ((throwAction != null && throwAction.WasPressedThisFrame()) ||
                 (attackAction != null && attackAction.WasPressedThisFrame())))
            {
                ThrowHeldObject();
            }
        }

        private void FixedUpdate()
        {
            if (heldObject != null && holdPoint != null)
            {
                heldObject.MoveHeldObject(
                    ResolveHoldPosition(holdPoint.position),
                    holdPoint.rotation);
            }
        }

        public bool TryHold(Throwable target)
        {
            if (target == null || heldObject != null || holdPoint == null)
            {
                return false;
            }

            heldObject = target;
            heldObject.BeginHold(this, playerColliders);
            return true;
        }

        /// <summary>Called by PlayerInput's Send Messages behavior.</summary>
        public void OnThrow(InputValue value)
        {
            if (value.isPressed)
            {
                ThrowHeldObject();
            }
        }

        /// <summary>Primary fire throws the held object instead of firing the rifle.</summary>
        public void OnAttack(InputValue value)
        {
            if (value.isPressed)
            {
                ThrowHeldObject();
            }
        }

        private void ThrowHeldObject()
        {
            if (heldObject == null || holdPoint == null)
            {
                return;
            }

            Vector3 releasePosition = ResolveHoldPosition(holdPoint.position);
            Throwable releasedObject = heldObject;
            heldObject = null;
            LastThrowFrame = Time.frameCount;
            releasedObject.Release(
                playerCamera != null ? playerCamera.transform.forward : holdPoint.forward,
                throwImpulse,
                playerColliders,
                releasePosition);
        }

        private Vector3 ResolveHoldPosition(Vector3 desiredPosition)
        {
            if (playerCamera == null || heldObject == null)
            {
                return desiredPosition;
            }

            Vector3 origin = playerCamera.transform.position;
            Vector3 offset = desiredPosition - origin;
            float distance = offset.magnitude;
            if (distance < 0.001f)
            {
                return desiredPosition;
            }

            Vector3 direction = offset / distance;
            int count = Physics.SphereCastNonAlloc(
                origin, 0.28f, direction, obstructionHits, distance,
                ~(1 << GameLayers.Player), QueryTriggerInteraction.Ignore);
            float allowedDistance = distance;
            for (int i = 0; i < count; i++)
            {
                Collider collider = obstructionHits[i].collider;
                if (collider == null ||
                    collider.transform.IsChildOf(transform) ||
                    collider.transform.IsChildOf(heldObject.transform))
                {
                    continue;
                }

                allowedDistance = Mathf.Min(
                    allowedDistance,
                    Mathf.Max(0f, obstructionHits[i].distance - 0.05f));
            }

            return origin + direction * allowedDistance;
        }

        private void RefreshActions()
        {
            InputActionAsset actions = playerInput != null
                ? playerInput.actions
                : null;
            if (actions == playerActions)
            {
                return;
            }

            playerActions = actions;
            InputActionMap playerMap = actions != null
                ? actions.FindActionMap("Player", false)
                : null;
            throwAction = playerMap?.FindAction("Throw", false);
            attackAction = playerMap?.FindAction("Attack", false);
        }

        private void OnDisable()
        {
            if (heldObject == null)
            {
                return;
            }

            Throwable releasedObject = heldObject;
            heldObject = null;
            releasedObject.Release(
                Vector3.zero, 0f, playerColliders,
                releasedObject.transform.position);
        }

        private void OnValidate()
        {
            throwImpulse = Mathf.Max(0.1f, throwImpulse);
        }
    }
}
