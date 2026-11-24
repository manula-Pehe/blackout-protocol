using System;
using BlackoutProtocol.Audio;
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
        [SerializeField] private Renderer secondaryIndicatorRenderer;
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

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
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

            audioSource.PlayOneShot(activationClip != null
                ? activationClip
                : ProceduralSfx.Click);

            Activated?.Invoke(user);
            onShutdownActivated?.Invoke();

            Debug.Log(
                $"{name} activated: facility shutdown objective complete.",
                this);
        }

        private void UpdateIndicator()
        {
            Color colour = IsActivated ? activatedColour : readyColour;
            UpdateIndicatorRenderer(indicatorRenderer, colour);
            UpdateIndicatorRenderer(secondaryIndicatorRenderer, colour);
        }

        private static void UpdateIndicatorRenderer(Renderer renderer, Color colour)
        {
            if (renderer != null)
            {
                Material material = renderer.material;
                material.color = colour;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", colour * 2f);
                }
            }
        }
    }
}
