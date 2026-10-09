using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackoutProtocol.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;

        [Header("Jumping and Gravity")]
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;

        private CharacterController characterController;
        private Vector2 moveInput;
        private float verticalVelocity;
        private bool sprintHeld;
        private bool jumpRequested;

        public bool IsRunning { get; private set; }
        public bool IsCrouching => false;
        public Vector3 Velocity => characterController.velocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            HandleMovement();
        }

        public void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>();
        }

        public void OnSprint(InputValue value)
        {
            sprintHeld = value.isPressed;
        }

        public void OnJump(InputValue value)
        {
            if (value.isPressed)
            {
                jumpRequested = true;
            }
        }

        private void HandleMovement()
        {
            Vector3 horizontalMovement =
                transform.right * moveInput.x +
                transform.forward * moveInput.y;

            if (horizontalMovement.sqrMagnitude > 1f)
            {
                horizontalMovement.Normalize();
            }

            IsRunning = sprintHeld && moveInput.sqrMagnitude > 0.01f;
            float currentSpeed = IsRunning ? sprintSpeed : walkSpeed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (jumpRequested && characterController.isGrounded)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            jumpRequested = false;
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 finalMovement =
                horizontalMovement * currentSpeed +
                Vector3.up * verticalVelocity;

            characterController.Move(finalMovement * Time.deltaTime);
        }
    }
}