using BlackoutProtocol.Core;
using BlackoutProtocol.Player;
using UnityEngine;

namespace BlackoutProtocol.Interaction
{
    /// <summary>
    /// Lightweight object that can be picked up, thrown, and heard by AI on impact.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class Throwable : MonoBehaviour, IInteractable
    {
        [Header("Physics")]
        [SerializeField, Min(0.1f)] private float mass = 2f;
        [SerializeField, Min(0.1f)] private float minimumImpactSpeed = 2.5f;
        [SerializeField, Min(0.05f)] private float impactCooldown = 0.25f;

        [Header("Optional audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip impactClip;

        [Header("Debug")]
        [SerializeField] private bool logImpactNoise;

        private Rigidbody body;
        private Collider[] objectColliders;
        private PlayerHold holder;
        private float nextImpactTime;
        private bool impactsArmed;

        public string Prompt => holder == null
            ? "Press E to pick up"
            : "Press Q to throw";

        public bool IsHeld => holder != null;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            objectColliders = GetComponentsInChildren<Collider>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void Interact(GameObject user)
        {
            if (holder != null || user == null)
            {
                return;
            }

            PlayerHold playerHold = user.GetComponent<PlayerHold>();
            playerHold?.TryHold(this);
        }

        public void BeginHold(PlayerHold newHolder, Collider[] holderColliders)
        {
            if (newHolder == null || holder != null)
            {
                return;
            }

            holder = newHolder;
            impactsArmed = false;
            SetCollisionWithHolder(holderColliders, true);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
        }

        public void MoveHeldObject(Vector3 position, Quaternion rotation)
        {
            if (holder == null)
            {
                return;
            }

            body.MovePosition(position);
            body.MoveRotation(rotation);
        }

        public void Release(
            Vector3 direction,
            float impulse,
            Collider[] holderColliders)
        {
            if (holder == null)
            {
                return;
            }

            holder = null;
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            SetCollisionWithHolder(holderColliders, false);
            impactsArmed = true;
            nextImpactTime = Time.time + 0.05f;

            if (direction.sqrMagnitude > 0.001f && impulse > 0f)
            {
                body.AddForce(
                    direction.normalized * impulse,
                    ForceMode.Impulse);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!impactsArmed || Time.time < nextImpactTime)
            {
                return;
            }

            float impactSpeed = collision.relativeVelocity.magnitude;

            if (impactSpeed < minimumImpactSpeed)
            {
                return;
            }

            nextImpactTime = Time.time + impactCooldown;
            float loudness = Mathf.Min(
                NoiseLevels.Impact,
                impactSpeed * 2f);

            GameEvents.RaiseNoise(new NoiseEvent(
                transform.position,
                loudness,
                NoiseType.Impact,
                Time.time,
                gameObject));

            if (audioSource != null && impactClip != null)
            {
                audioSource.PlayOneShot(impactClip);
            }

            if (logImpactNoise)
            {
                Debug.Log(
                    $"{name} impact noise: range={loudness:F1}m, " +
                    $"speed={impactSpeed:F1}m/s",
                    this);
            }
        }

        private void SetCollisionWithHolder(
            Collider[] holderColliders,
            bool ignore)
        {
            if (holderColliders == null)
            {
                return;
            }

            foreach (Collider objectCollider in objectColliders)
            {
                foreach (Collider holderCollider in holderColliders)
                {
                    if (objectCollider != null && holderCollider != null)
                    {
                        Physics.IgnoreCollision(
                            objectCollider,
                            holderCollider,
                            ignore);
                    }
                }
            }
        }

        private void OnValidate()
        {
            mass = Mathf.Max(0.1f, mass);
            minimumImpactSpeed = Mathf.Max(0.1f, minimumImpactSpeed);
            impactCooldown = Mathf.Max(0.05f, impactCooldown);
        }
    }
}
