using BlackoutProtocol.Audio;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Interaction
{
    public sealed class SlidingDoor : MonoBehaviour, IInteractable, IDoor
    {
        [Header("Door Movement")]
        [SerializeField] private Rigidbody doorPanel;
        [SerializeField] private Vector3 localOpenOffset =
            new Vector3(2.2f, 0f, 0f);
        [SerializeField] private float movementSpeed = 2f;
        [SerializeField] private float arrivalDistance = 0.01f;

        [Header("Door State")]
        [SerializeField] private bool startsLocked;

        [Header("Optional Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openingClip;
        [SerializeField] private AudioClip closingClip;

        [Header("Debug")]
        [SerializeField] private bool logNoiseEvents;

        private Vector3 closedPosition;
        private Vector3 openPosition;
        private bool wantsToBeOpen;
        private bool isMoving;
        private Collider levelCollider;

        public bool IsOpen { get; private set; }
        public bool IsLocked { get; private set; }
        public Vector3 Position => transform.position;

        public string Prompt
        {
            get
            {
                if (IsLocked)
                {
                    return "Door locked";
                }

                if (isMoving)
                {
                    return wantsToBeOpen ? "Opening..." : "Closing...";
                }

                return wantsToBeOpen
                    ? "Press E to close"
                    : "Press E to open";
            }
        }

        private void Awake()
        {
            if (doorPanel == null)
            {
                Debug.LogError(
                    $"{name} requires a door panel Rigidbody.",
                    this);

                enabled = false;
                return;
            }

            BuildWindowedPanel();

            closedPosition = doorPanel.position;
            openPosition =
                closedPosition +
                transform.TransformVector(localOpenOffset);

            IsLocked = startsLocked;
            levelCollider = doorPanel.GetComponent<Collider>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 1.5f;
            audioSource.maxDistance = 18f;
            audioSource.volume = 0.45f;
        }

        private void BuildWindowedPanel()
        {
            MeshRenderer panelRenderer = doorPanel.GetComponent<MeshRenderer>();
            if (panelRenderer == null) return;
            Material doorMaterial = panelRenderer.sharedMaterial;
            panelRenderer.enabled = false;
            AddFramePart("Window Frame Left", new Vector3(-0.375f, 0f, 0f),
                new Vector3(0.25f, 1f, 1f), doorMaterial);
            AddFramePart("Window Frame Right", new Vector3(0.375f, 0f, 0f),
                new Vector3(0.25f, 1f, 1f), doorMaterial);
            AddFramePart("Window Frame Bottom", new Vector3(0f, -0.325f, 0f),
                new Vector3(0.5f, 0.35f, 1f), doorMaterial);
            AddFramePart("Window Frame Top", new Vector3(0f, 0.325f, 0f),
                new Vector3(0.5f, 0.35f, 1f), doorMaterial);
        }

        private void AddFramePart(string partName, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.layer = doorPanel.gameObject.layer;
            part.transform.SetParent(doorPanel.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider partCollider = part.GetComponent<Collider>();
            partCollider.enabled = false;
            Destroy(partCollider);
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void FixedUpdate()
        {
            if (!isMoving || doorPanel == null)
            {
                return;
            }

            Vector3 targetPosition =
                wantsToBeOpen ? openPosition : closedPosition;

            Vector3 nextPosition = Vector3.MoveTowards(
                doorPanel.position,
                targetPosition,
                movementSpeed * Time.fixedDeltaTime);

            doorPanel.MovePosition(nextPosition);

            float remainingDistance =
                Vector3.Distance(nextPosition, targetPosition);

            if (remainingDistance <= arrivalDistance)
            {
                doorPanel.MovePosition(targetPosition);
                isMoving = false;

                bool stateChanged = IsOpen != wantsToBeOpen;
                IsOpen = wantsToBeOpen;

                if (stateChanged)
                {
                    GameEvents.RaiseLevelChanged(levelCollider);
                }
            }
        }

        public void Interact(GameObject user)
        {
            if (IsLocked)
            {
                Debug.Log($"{name} is locked.", this);
                return;
            }

            if (wantsToBeOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (IsLocked)
            {
                return;
            }

            if (wantsToBeOpen && (isMoving || IsOpen))
            {
                return;
            }

            wantsToBeOpen = true;
            BeginMovement(openingClip, "opening");
        }

        public void Close()
        {
            if (!wantsToBeOpen && (isMoving || !IsOpen))
            {
                return;
            }

            wantsToBeOpen = false;
            BeginMovement(closingClip, "closing");
        }

        private void BeginMovement(AudioClip clip, string movementName)
        {
            isMoving = true;

            GameEvents.RaiseNoise(new NoiseEvent(
                transform.position,
                NoiseLevels.Door,
                NoiseType.Door,
                Time.time,
                gameObject));

            if (audioSource != null) audioSource.PlayOneShot(clip != null ? clip : ProceduralSfx.Door);

            if (logNoiseEvents)
            {
                Debug.Log(
                    $"{name} {movementName}: door noise range=" +
                    $"{NoiseLevels.Door:F0}m",
                    this);
            }
        }

        public void Lock()
        {
            IsLocked = true;
        }

        public void Unlock()
        {
            IsLocked = false;
        }

        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0.1f, movementSpeed);
            arrivalDistance = Mathf.Max(0.001f, arrivalDistance);
        }
    }
}
