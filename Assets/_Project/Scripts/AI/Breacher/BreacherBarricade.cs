using UnityEngine;

namespace BlackoutProtocol.AI.Breacher
{
    /// <summary>
    /// A kinematic obstacle the Breacher can push out of a route.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BreacherBarricade : MonoBehaviour
    {
        [Header("Push Movement")]
        [SerializeField] private Vector3 localPushOffset =
            new Vector3(0f, 0f, 2f);
        [SerializeField] private float pushSpeed = 2f;
        [SerializeField] private float arrivalDistance = 0.02f;

        private Rigidbody barricadeBody;
        private Vector3 clearedPosition;
        private bool isMoving;

        public bool IsCleared { get; private set; }
        public Vector3 Position => transform.position;

        private void Awake()
        {
            barricadeBody = GetComponent<Rigidbody>();
            barricadeBody.isKinematic = true;
            clearedPosition = barricadeBody.position +
                transform.TransformVector(localPushOffset);
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
            barricadeBody.MovePosition(nextPosition);

            if (Vector3.Distance(nextPosition, clearedPosition) <=
                arrivalDistance)
            {
                barricadeBody.MovePosition(clearedPosition);
                isMoving = false;
                IsCleared = true;
            }
        }

        [ContextMenu("Debug/Push Barricade")]
        public void Push()
        {
            if (!IsCleared)
            {
                isMoving = true;
            }
        }

        private void OnValidate()
        {
            pushSpeed = Mathf.Max(0.1f, pushSpeed);
            arrivalDistance = Mathf.Max(0.001f, arrivalDistance);
        }
    }
}
