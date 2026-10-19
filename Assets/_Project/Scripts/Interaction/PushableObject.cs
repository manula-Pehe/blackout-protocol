using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.Interaction
{
    /// <summary>
    /// Stable physics object that can be pushed by agents or by the player.
    /// NavMesh carving is restored only after the object settles.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class PushableObject : MonoBehaviour, IPushable
    {
        [Header("Physics")]
        [SerializeField, Min(0.1f)] private float mass = 35f;
        [SerializeField, Min(0f)] private float linearDamping = 2f;
        [SerializeField, Min(0f)] private float angularDamping = 4f;
        [SerializeField, Min(0.01f)] private float agentPushMultiplier = 1f;

        [Header("Settling")]
        [SerializeField, Min(0.01f)] private float settledSpeed = 0.08f;
        [SerializeField, Min(0.01f)] private float settleDuration = 0.5f;
        [SerializeField, Min(0.001f)] private float meaningfulMovement = 0.05f;

        [Header("Optional Audio")]
        [SerializeField] private AudioSource scrapeAudioSource;
        [SerializeField] private AudioClip scrapeClip;

        [Header("Debug")]
        [SerializeField] private bool logLevelChanges;

        private Rigidbody body;
        private Collider levelCollider;
        private NavMeshObstacle navMeshObstacle;
        private Vector3 previousPosition;
        private Vector3 lastSettledPosition;
        private float stationaryTimer;
        private bool movementInProgress;

        public bool CanPush => body != null &&
            body.gameObject.activeInHierarchy &&
            !body.isKinematic;

        public Vector3 Position => body != null
            ? body.position
            : transform.position;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            levelCollider = GetComponent<Collider>();
            navMeshObstacle = GetComponent<NavMeshObstacle>();

            body.mass = mass;
            body.linearDamping = linearDamping;
            body.angularDamping = angularDamping;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.constraints |= RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;

            if (navMeshObstacle != null)
            {
                navMeshObstacle.carving = true;
                navMeshObstacle.carveOnlyStationary = true;
            }

            previousPosition = body.position;
            lastSettledPosition = body.position;
        }

        private void FixedUpdate()
        {
            float movementThisStep = Vector3.Distance(
                body.position,
                previousPosition);
            previousPosition = body.position;

            bool moving = body.linearVelocity.sqrMagnitude >
                    settledSpeed * settledSpeed ||
                body.angularVelocity.sqrMagnitude >
                    settledSpeed * settledSpeed ||
                movementThisStep > 0.001f;

            if (moving)
            {
                BeginMovement();
                stationaryTimer = 0f;
                return;
            }

            if (!movementInProgress)
            {
                return;
            }

            stationaryTimer += Time.fixedDeltaTime;

            if (stationaryTimer < settleDuration)
            {
                return;
            }

            FinishMovement();
        }

        public void Push(Vector3 direction, float force)
        {
            if (!CanPush || direction.sqrMagnitude < 0.001f || force <= 0f)
            {
                return;
            }

            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            BeginMovement();
            body.AddForce(
                direction.normalized * force * agentPushMultiplier,
                ForceMode.Impulse);
        }

        private void BeginMovement()
        {
            if (!movementInProgress)
            {
                movementInProgress = true;

                GameEvents.RaiseNoise(new NoiseEvent(
                    transform.position,
                    NoiseLevels.Impact * 0.5f,
                    NoiseType.Impact,
                    Time.time,
                    gameObject));

                if (logLevelChanges)
                {
                    Debug.Log(
                        $"{name} started moving: scrape noise range=" +
                        $"{NoiseLevels.Impact * 0.5f:F0}m",
                        this);
                }

                if (scrapeAudioSource != null && scrapeClip != null)
                {
                    scrapeAudioSource.clip = scrapeClip;
                    scrapeAudioSource.loop = true;
                    scrapeAudioSource.Play();
                }

                if (navMeshObstacle != null)
                {
                    navMeshObstacle.carving = false;
                }
            }
        }

        private void FinishMovement()
        {
            movementInProgress = false;
            stationaryTimer = 0f;
            body.Sleep();

            if (scrapeAudioSource != null)
            {
                scrapeAudioSource.Stop();
            }

            if (navMeshObstacle != null)
            {
                navMeshObstacle.carving = true;
            }

            if (Vector3.Distance(body.position, lastSettledPosition) >=
                meaningfulMovement)
            {
                lastSettledPosition = body.position;
                GameEvents.RaiseLevelChanged(levelCollider);

                if (logLevelChanges)
                {
                    Debug.Log(
                        $"{name} settled and raised LevelChanged.",
                        this);
                }
            }
        }

        private void OnValidate()
        {
            mass = Mathf.Max(0.1f, mass);
            linearDamping = Mathf.Max(0f, linearDamping);
            angularDamping = Mathf.Max(0f, angularDamping);
            agentPushMultiplier = Mathf.Max(0.01f, agentPushMultiplier);
            settledSpeed = Mathf.Max(0.01f, settledSpeed);
            settleDuration = Mathf.Max(0.01f, settleDuration);
            meaningfulMovement = Mathf.Max(0.001f, meaningfulMovement);
        }
    }
}
