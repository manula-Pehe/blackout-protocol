using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackoutProtocol.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float crouchSpeed = 2f;

        [Header("Jumping and Gravity")]
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;

        [Header("Crouching")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float crouchingHeight = 1.2f;
        [SerializeField] private float standingCameraHeight = 0.65f;
        [SerializeField] private float crouchingCameraHeight = 0.25f;
        [SerializeField] private float crouchTransitionSpeed = 12f;

        private CharacterController characterController;
        private Vector2 moveInput;
        private float verticalVelocity;
        private bool sprintHeld;
        private bool crouchToggled;
        private bool jumpRequested;

        public bool IsRunning { get; private set; }
        public bool IsCrouching { get; private set; }
        public Vector3 Velocity => characterController.velocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            HandleCrouching();
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

        public void OnCrouch(InputValue value)
        {
            if (value.isPressed)
            {
                crouchToggled = !crouchToggled;
            }
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

            IsRunning =
                sprintHeld &&
                !IsCrouching &&
                moveInput.sqrMagnitude > 0.01f;

            float currentSpeed;

            if (IsCrouching)
            {
                currentSpeed = crouchSpeed;
            }
            else if (IsRunning)
            {
                currentSpeed = sprintSpeed;
            }
            else
            {
                currentSpeed = walkSpeed;
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (jumpRequested &&
                characterController.isGrounded &&
                !IsCrouching)
            {
                verticalVelocity = Mathf.Sqrt(
                    jumpHeight * -2f * gravity);
            }

            jumpRequested = false;
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 finalMovement =
                horizontalMovement * currentSpeed +
                Vector3.up * verticalVelocity;

            characterController.Move(finalMovement * Time.deltaTime);
        }

        private void HandleCrouching()
        {
            IsCrouching = crouchToggled;

            float targetHeight =
                IsCrouching ? crouchingHeight : standingHeight;

            float newHeight = Mathf.Lerp(
                characterController.height,
                targetHeight,
                crouchTransitionSpeed * Time.deltaTime);

            characterController.height = newHeight;

            // Keep the bottom of the controller in the same position.
            characterController.center = new Vector3(
                0f,
                (newHeight - standingHeight) * 0.5f,
                0f);

            float targetCameraHeight =
                IsCrouching
                    ? crouchingCameraHeight
                    : standingCameraHeight;

            Vector3 cameraPosition = playerCamera.localPosition;
            cameraPosition.y = Mathf.Lerp(
                cameraPosition.y,
                targetCameraHeight,
                crouchTransitionSpeed * Time.deltaTime);

            playerCamera.localPosition = cameraPosition;
        }
    }
}
