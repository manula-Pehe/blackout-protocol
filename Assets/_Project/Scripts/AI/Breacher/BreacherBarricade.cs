using BlackoutProtocol.Audio;
using BlackoutProtocol.Core;
using BlackoutProtocol.Interaction;
using UnityEngine;

namespace BlackoutProtocol.AI.Breacher
{
    /// <summary>
    /// A kinematic obstacle the Breacher can push out of a route.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BreacherBarricade : MonoBehaviour, IPushable, IInteractable
    {
        [Header("Push Movement")]
        [SerializeField] private Vector3 localPushOffset =
            new Vector3(0f, 0f, 2f);
        [SerializeField] private float pushSpeed = 2f;
        [SerializeField] private float arrivalDistance = 0.02f;

        private Rigidbody barricadeBody;
        private Collider levelCollider;
        private Vector3 clearedPosition;
        private bool isMoving;
        private AudioSource scrapeSource;

        public bool IsCleared { get; private set; }
        public bool IsBlocked { get; private set; }
        public bool CanPush => !IsCleared && !IsBlocked;
        public Vector3 Position => transform.position;
        public string Prompt => IsCleared ? string.Empty :
            IsBlocked ? "Crate is blocked" : "Press E to push crate";

        private void Awake()
        {
            barricadeBody = GetComponent<Rigidbody>();
            levelCollider = GetComponent<Collider>();
            barricadeBody.isKinematic = true;
            clearedPosition = barricadeBody.position +
                transform.TransformDirection(localPushOffset);
            scrapeSource = gameObject.AddComponent<AudioSource>();
            scrapeSource.playOnAwake = false;
            scrapeSource.spatialBlend = 1f;
            scrapeSource.minDistance = 1.5f;
            scrapeSource.maxDistance = 18f;
            scrapeSource.volume = 0.45f;
            scrapeSource.loop = true;
            scrapeSource.clip = ProceduralSfx.Scrape;
        }

        private void FixedUpdate()
        {
            if (!isMoving)
            {
                return;
            }

            Vector3 nextPosition = Vector3.MoveTowards(
                barricadeBody.position,
                clearedPosition,
                pushSpeed * Time.fixedDeltaTime);

            Vector3 movement = nextPosition - barricadeBody.position;

            if (movement.sqrMagnitude > 0f &&
                barricadeBody.SweepTest(
                    movement.normalized,
                    out _,
                    movement.magnitude + 0.01f,
                    QueryTriggerInteraction.Ignore))
            {
                isMoving = false;
                IsBlocked = true;
                scrapeSource.Stop();
                Debug.LogWarning(
                    $"{name} cannot be pushed further.",
                    this);
                return;
            }

            barricadeBody.MovePosition(nextPosition);

            if (Vector3.Distance(nextPosition, clearedPosition) <=
                arrivalDistance)
            {
                barricadeBody.MovePosition(clearedPosition);
                isMoving = false;
                IsCleared = true;
                scrapeSource.Stop();
                GameEvents.RaiseLevelChanged(levelCollider);
            }
        }

        [ContextMenu("Debug/Push Barricade")]
        public void Push()
        {
            Vector3 worldOffset =
                transform.TransformDirection(localPushOffset);
            Push(worldOffset.normalized, worldOffset.magnitude);
        }

        public void Interact(GameObject user)
        {
            if (user != null && !IsCleared)
            {
                Push();
            }
        }

        public void Push(Vector3 direction, float force)
        {
            if (IsCleared || direction.sqrMagnitude < 0.001f || force <= 0f)
            {
                return;
            }

            IsBlocked = false;
            clearedPosition = barricadeBody.position +
                direction.normalized * force;
            isMoving = true;
            if (!scrapeSource.isPlaying) scrapeSource.Play();
        }

        private void OnDisable()
        {
            if (scrapeSource != null) scrapeSource.Stop();
        }

        private void OnValidate()
        {
            pushSpeed = Mathf.Max(0.1f, pushSpeed);
            arrivalDistance = Mathf.Max(0.001f, arrivalDistance);
        }
    }
}
