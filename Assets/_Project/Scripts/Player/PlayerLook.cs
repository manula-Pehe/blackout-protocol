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
        [Header("Weapon Recoil")]
        [SerializeField, Min(0.01f)] private float recoilReturnTime = 0.12f;

        private Vector2 lookInput;
        private float pitch;
        private int lookFramesToIgnore;
        private float recoilPitch;
        private float recoilPitchVelocity;
        private float recoilRoll;
        private float recoilRollVelocity;

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

            recoilPitch = Mathf.SmoothDamp(recoilPitch, 0f,
                ref recoilPitchVelocity, recoilReturnTime);
            recoilRoll = Mathf.SmoothDamp(recoilRoll, 0f,
                ref recoilRollVelocity, recoilReturnTime);

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                lookInput = Vector2.zero;
                ApplyCameraRotation();
                return;
            }

            if (lookFramesToIgnore > 0)
            {
                lookFramesToIgnore--;
                lookInput = Vector2.zero;
                ApplyCameraRotation();
                return;
            }

            float yawChange = lookInput.x * mouseSensitivity;
            float pitchChange = lookInput.y * mouseSensitivity;

            transform.Rotate(Vector3.up, yawChange);

            pitch -= pitchChange;
            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);

            ApplyCameraRotation();
        }

        private void ApplyCameraRotation()
        {
            playerCamera.localRotation = Quaternion.Euler(
                pitch - recoilPitch, 0f, recoilRoll);
        }

        /// <summary>Adds a short camera kick without changing the player's saved aim pitch.</summary>
        public void AddRecoil(float upwardDegrees, float rollDegrees)
        {
            recoilPitch += upwardDegrees;
            recoilRoll += rollDegrees;
        }

        public void OnLook(InputValue value)
        {
            lookInput = value.Get<Vector2>();
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            lookInput = Vector2.zero;
            lookFramesToIgnore = 2;
        }
    }
}
