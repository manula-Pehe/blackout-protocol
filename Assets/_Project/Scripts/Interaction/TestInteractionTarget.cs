using UnityEngine;

namespace BlackoutProtocol.Interaction
{
    public sealed class TestInteractionTarget :
        MonoBehaviour,
        IInteractable
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color inactiveColor = Color.red;
        [SerializeField] private Color activeColor = Color.green;

        private bool isActive;

        public string Prompt =>
            isActive ? "Press E to deactivate" : "Press E to activate";

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            UpdateColour();
        }

        public void Interact(GameObject user)
        {
            isActive = !isActive;
            UpdateColour();

            Debug.Log(
                $"{user.name} interacted with {gameObject.name}. " +
                $"Active: {isActive}");
        }

        private void UpdateColour()
        {
            if (targetRenderer != null)
            {
                targetRenderer.material.color =
                    isActive ? activeColor : inactiveColor;
            }
        }
    }
}