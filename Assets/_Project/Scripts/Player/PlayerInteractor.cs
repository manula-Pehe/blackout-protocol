using BlackoutProtocol.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackoutProtocol.Player
{
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactionDistance = 3f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private IInteractable currentInteractable;

        public string CurrentPrompt =>
            currentInteractable?.Prompt ?? string.Empty;

        private void Update()
        {
            FindCurrentInteractable();
        }

        public void OnInteract(InputValue value)
        {
            if (value.isPressed && currentInteractable != null)
            {
                currentInteractable.Interact(gameObject);
            }
        }

        private void FindCurrentInteractable()
        {
            currentInteractable = null;

            Ray ray = new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward);

            bool foundObject = Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactionDistance,
                interactionMask,
                QueryTriggerInteraction.Ignore);

            if (!foundObject)
            {
                return;
            }

            currentInteractable =
                hit.collider.GetComponentInParent<IInteractable>();
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0.1f, interactionDistance);
        }
    }
}
