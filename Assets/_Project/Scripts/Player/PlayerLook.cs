using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackoutProtocol.Player
{
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform playerCamera;
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float minimumPitch = -85f;
        [SerializeField] private float maximumPitch = 85f;

        private Vector2 lookInput;
        private float pitch;

        private void Start()
        {
            LockCursor();
        }

        private void Update()
        {
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor();
            }

            float yawChange = lookInput.x * mouseSensitivity;
            float pitchChange = lookInput.y * mouseSensitivity;

            transform.Rotate(Vector3.up, yawChange);

            pitch -= pitchChange;
            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);

            playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void OnLook(InputValue value)
        {
            lookInput = value.Get<Vector2>();
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}