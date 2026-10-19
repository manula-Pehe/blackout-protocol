using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Player
{
    /// <summary>
    /// Emits shared AI-hearing events as the grounded player travels.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FootstepEmitter : MonoBehaviour
    {
        [Header("Step spacing")]
        [SerializeField, Min(0.1f)] private float walkingStepDistance = 1.8f;
        [SerializeField, Min(0.1f)] private float runningStepDistance = 2.2f;
        [SerializeField, Min(0f)] private float minimumMovingSpeed = 0.2f;

        [Header("Optional audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip walkingClip;
        [SerializeField] private AudioClip runningClip;

        [Header("Surface Detection")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(0.1f)] private float groundRayDistance = 2f;

        [Header("Debug")]
        [SerializeField] private bool logNoiseEvents;

        private FirstPersonController playerController;
        private CharacterController characterController;
        private Vector3 previousPosition;
        private float distanceSinceLastStep;

        private void Awake()
        {
            playerController = GetComponent<FirstPersonController>();
            characterController = GetComponent<CharacterController>();
            previousPosition = transform.position;
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            distanceSinceLastStep = 0f;
        }

        private void Update()
        {
            Vector3 currentPosition = transform.position;
            Vector3 movement = currentPosition - previousPosition;
            movement.y = 0f;
            previousPosition = currentPosition;

            float horizontalSpeed = new Vector3(
                playerController.Velocity.x,
                0f,
                playerController.Velocity.z).magnitude;

            if (!characterController.isGrounded ||
                horizontalSpeed < minimumMovingSpeed)
            {
                return;
            }

            // Crouching is deliberately silent to the AI. Resetting the
            // accumulator prevents an immediate loud step on standing up.
            if (playerController.IsCrouching)
            {
                distanceSinceLastStep = 0f;
                return;
            }

            distanceSinceLastStep += movement.magnitude;
            float requiredDistance = playerController.IsRunning
                ? runningStepDistance
                : walkingStepDistance;

            if (distanceSinceLastStep < requiredDistance)
            {
                return;
            }

            distanceSinceLastStep %= requiredDistance;
            EmitFootstep(playerController.IsRunning);
        }

        private void EmitFootstep(bool running)
        {
            float loudness = running
                ? NoiseLevels.RunningFootstep
                : NoiseLevels.WalkingFootstep;
            AudioClip clip = running ? runningClip : walkingClip;
            string surfaceName = "Default";

            if (TryGetSurface(out FootstepSurface surface))
            {
                loudness *= surface.NoiseMultiplier;
                surfaceName = surface.SurfaceName;
                clip = surface.GetClip(running) ?? clip;
            }

            GameEvents.RaiseNoise(new NoiseEvent(
                transform.position,
                loudness,
                NoiseType.Footstep,
                Time.time,
                gameObject));

            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }

            if (logNoiseEvents)
            {
                Debug.Log(
                    $"Footstep noise: {(running ? "Running" : "Walking")}, " +
                    $"surface={surfaceName}, range={loudness:F1}m",
                    this);
            }
        }

        private bool TryGetSurface(out FootstepSurface surface)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;

            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    groundRayDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                surface = hit.collider.GetComponentInParent<FootstepSurface>();
                return surface != null;
            }

            surface = null;
            return false;
        }

        private void OnValidate()
        {
            walkingStepDistance = Mathf.Max(0.1f, walkingStepDistance);
            runningStepDistance = Mathf.Max(0.1f, runningStepDistance);
            minimumMovingSpeed = Mathf.Max(0f, minimumMovingSpeed);
            groundRayDistance = Mathf.Max(0.1f, groundRayDistance);
        }
    }
}
