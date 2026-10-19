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

        public bool IsHoldingObject => heldObject != null;

        private void Awake()
        {
            playerColliders = GetComponentsInChildren<Collider>();
        }

        private void FixedUpdate()
        {
            if (heldObject != null && holdPoint != null)
            {
                heldObject.MoveHeldObject(
                    holdPoint.position,
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
            if (!value.isPressed || heldObject == null)
            {
                return;
            }

            Throwable releasedObject = heldObject;
            heldObject = null;
            releasedObject.Release(
                holdPoint.forward,
                throwImpulse,
                playerColliders);
        }

        private void OnDisable()
        {
            if (heldObject == null)
            {
                return;
            }

            Throwable releasedObject = heldObject;
            heldObject = null;
            releasedObject.Release(Vector3.zero, 0f, playerColliders);
        }

        private void OnValidate()
        {
            throwImpulse = Mathf.Max(0.1f, throwImpulse);
        }
    }
}
