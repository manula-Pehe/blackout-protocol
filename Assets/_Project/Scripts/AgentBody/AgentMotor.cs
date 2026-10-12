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

        private CharacterController controller;
        private float verticalSpeed;

        /// <summary>Horizontal velocity actually achieved last frame, in m/s.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>True when the controller touched the ground on the last move.</summary>
        public bool IsGrounded => controller != null && controller.isGrounded;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        /// <summary>Moves the agent with the given horizontal velocity for one frame.</summary>
        public void Move(Vector3 horizontalVelocity, float deltaTime)
        {
            if (deltaTime <= 0f || !controller.enabled)
            {
                Velocity = Vector3.zero;
                return;
            }

            if (controller.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = groundedStickSpeed;
            }
            else
            {
                verticalSpeed += gravity * deltaTime;
            }

            Vector3 before = transform.position;

            Vector3 motion = horizontalVelocity * deltaTime;
            motion.y = verticalSpeed * deltaTime;
            controller.Move(motion);

            Vector3 delta = transform.position - before;
            delta.y = 0f;
            Velocity = delta / deltaTime;
        }

        /// <summary>Teleports the agent (the controller must be disabled to set position).</summary>
        public void Warp(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalSpeed = 0f;
            Velocity = Vector3.zero;
        }
    }
}
