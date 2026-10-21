using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>
    /// Applies movement through a <see cref="CharacterController"/>. The body decides the
    /// horizontal velocity; the motor adds gravity, handles collisions and reports the
    /// velocity that actually happened (used by the animator so feet match real speed).
    /// No NavMeshAgent is involved.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class AgentMotor : MonoBehaviour
    {
        [Tooltip("Downward acceleration in m/s².")]
        [SerializeField] private float gravity = -20f;

        [Tooltip("Small downward speed while grounded so the controller stays on slopes and steps.")]
        [SerializeField] private float groundedStickSpeed = -2f;

        [Tooltip("Maximum downward contact correction after leaving walkable ground. Zero disables snapping; never snaps up or across a large drop.")]
        [SerializeField, Range(0,0.5f)] private float groundSnapDistance = 0.25f;
        [SerializeField] private LayerMask groundMask = ~0;

        private CharacterController controller;
        private float verticalSpeed;
        private bool hasMoved;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];

        /// <summary>Successful bounded downward contact corrections, useful for sandbox evidence.</summary>
        public int GroundSnapCount { get; private set; }

        /// <summary>Horizontal velocity actually achieved last frame, in m/s.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>True when the controller touched the ground on the last move.</summary>
        public bool IsGrounded => controller != null && controller.isGrounded;

        // Stop/arrival must report rest immediately, even inside their event callbacks.
        // Do not reset gravity or move the controller as a side effect of cancellation.
        internal void ClearHorizontalVelocity() => Velocity = Vector3.zero;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            // Arrival/corner steering deliberately issues sub-millimetre steps at high
            // frame rates. Filtering them stalls valid motion and causes false Stuck.
            // Collision/skin width still constrain movement; no prefab migration needed.
            controller.minMoveDistance = 0f;
        }

        /// <summary>Moves the agent with the given horizontal velocity for one frame.</summary>
        public void Move(Vector3 horizontalVelocity, float deltaTime)
        {
            if (deltaTime <= 0f || !controller.enabled)
            {
                Velocity = Vector3.zero;
                return;
            }

            Vector3 before = transform.position;
            // A long frame must not carry a grounded capsule farther down a slope
            // than the bounded contact probe can follow. Normal 30+ FPS uses one
            // step; the cap bounds exceptional-frame physics work.
            int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / (1f / 30)), 1, 32);
            float stepTime = deltaTime / steps;
            for (int i = 0; i < steps; i++) MoveStep(horizontalVelocity, stepTime);

            Vector3 delta = transform.position - before;
            delta.y = 0f;
            Velocity = delta / deltaTime;
        }

        private void MoveStep(Vector3 horizontalVelocity, float deltaTime)
        {
            if (controller.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = groundedStickSpeed;
            }
            else
            {
                verticalSpeed += gravity * deltaTime;
            }

            bool wasGrounded = hasMoved && controller.isGrounded;

            Vector3 motion = horizontalVelocity * deltaTime;
            motion.y = verticalSpeed * deltaTime;
            controller.Move(motion);
            hasMoved = true;
            if (wasGrounded && !controller.isGrounded && verticalSpeed <= 0 && groundSnapDistance > 0)
                SnapToWalkableGround();

        }

        private void SnapToWalkableGround()
        {
            float snapDistance = Mathf.Clamp(groundSnapDistance,0,0.5f);
            float lift = controller.skinWidth + 0.05f;
            Vector3 foot = controller.bounds.center - Vector3.up * controller.bounds.extents.y;
            int count = Physics.RaycastNonAlloc(foot + Vector3.up * lift, Vector3.down, groundHits,
                lift + snapDistance, groundMask, QueryTriggerInteraction.Ignore);
            if (count == groundHits.Length) return; // Unknown nearest obstruction: fail closed.
            int nearest = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider hit = groundHits[i].collider;
                if (hit == controller || hit.transform.IsChildOf(transform) ||
                    Physics.GetIgnoreLayerCollision(gameObject.layer,hit.gameObject.layer) || Physics.GetIgnoreCollision(controller,hit)) continue;
                if (groundHits[i].distance < distance) { nearest=i; distance=groundHits[i].distance; }
            }
            if (nearest < 0) return;
            var ground = groundHits[nearest];
            // Do not skip a steep/agent obstruction to snap onto a floor beneath it.
            if (ground.normal.y < Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad) ||
                ground.collider is CharacterController || ground.collider.GetComponentInParent<AgentBody>() != null || distance < lift) return;
            float down = Mathf.Min(snapDistance, distance - lift + controller.skinWidth);
            if ((controller.Move(Vector3.down * down) & CollisionFlags.Below) != 0)
            {
                verticalSpeed = Mathf.Min(-0.01f,groundedStickSpeed);
                GroundSnapCount++;
            }
        }

        /// <summary>Teleports the agent (the controller must be disabled to set position).</summary>
        public void Warp(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalSpeed = 0f;
            hasMoved = false;
            Velocity = Vector3.zero;
        }
    }
}
