using UnityEngine;

namespace BlackoutProtocol.Interaction
{
    public sealed class SlidingDoor : MonoBehaviour, IInteractable
    {
        [Header("Door Movement")]
        [SerializeField] private Rigidbody doorPanel;
        [SerializeField] private Vector3 localOpenOffset =
            new Vector3(2.2f, 0f, 0f);
        [SerializeField] private float movementSpeed = 2f;
        [SerializeField] private float arrivalDistance = 0.01f;

        [Header("Door State")]
        [SerializeField] private bool startsLocked;

        private Vector3 closedPosition;
        private Vector3 openPosition;
        private bool wantsToBeOpen;
        private bool isMoving;

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

            closedPosition = doorPanel.position;
            openPosition =
                closedPosition +
                transform.TransformVector(localOpenOffset);

            IsLocked = startsLocked;
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
                IsOpen = wantsToBeOpen;
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

            wantsToBeOpen = true;
            isMoving = true;
        }

        public void Close()
        {
            wantsToBeOpen = false;
            isMoving = true;
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