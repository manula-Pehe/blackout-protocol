using System;
using UnityEngine;
using UnityEngine.Events;

namespace BlackoutProtocol.Interaction
{
    /// <summary>
    /// One-use final objective console. Other game-flow code can subscribe in C#
    /// or connect through the inspector event without a direct dependency.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class ShutdownConsole : MonoBehaviour, IInteractable
    {
        [Header("Visual Feedback")]
        [SerializeField] private Renderer indicatorRenderer;
        [SerializeField] private Color readyColour = new Color(0f, 0.7f, 1f);
        [SerializeField] private Color activatedColour = Color.green;

        [Header("Optional Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip activationClip;

        [Header("Game Flow")]
        [SerializeField] private UnityEvent onShutdownActivated;

        public event Action<GameObject> Activated;

        public bool IsActivated { get; private set; }

        public string Prompt => IsActivated
            ? "Facility shutdown complete"
            : "Press E to activate shutdown";

        private void Awake()
        {
            if (indicatorRenderer == null)
            {
                indicatorRenderer = GetComponent<Renderer>();
            }

            UpdateIndicator();
        }

        public void Interact(GameObject user)
        {
            if (IsActivated)
            {
                return;
            }

            IsActivated = true;
            UpdateIndicator();

            if (audioSource != null && activationClip != null)
            {
                audioSource.PlayOneShot(activationClip);
            }

            Activated?.Invoke(user);
            onShutdownActivated?.Invoke();

            Debug.Log(
                $"{name} activated: facility shutdown objective complete.",
                this);
        }

        private void UpdateIndicator()
        {
            if (indicatorRenderer != null)
            {
                indicatorRenderer.material.color = IsActivated
                    ? activatedColour
                    : readyColour;
            }
        }
    }
}
