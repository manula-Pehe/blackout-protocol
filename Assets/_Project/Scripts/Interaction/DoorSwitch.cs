using BlackoutProtocol.Audio;
using UnityEngine;

namespace BlackoutProtocol.Interaction
{
    public sealed class DoorSwitch : MonoBehaviour, IInteractable
    {
        [Header("Door")]
        [SerializeField] private SlidingDoor linkedDoor;
        [SerializeField] private bool openDoorOnUse = true;
        [SerializeField] private bool canOnlyBeUsedOnce = true;

        [Header("Visual Feedback")]
        [SerializeField] private Renderer indicatorRenderer;
        [SerializeField] private Color readyColour = Color.red;
        [SerializeField] private Color activatedColour = Color.green;

        private bool hasBeenUsed;
        private AudioSource clickSource;

        public string Prompt
        {
            get
            {
                if (hasBeenUsed && canOnlyBeUsedOnce)
                {
                    return "Switch already activated";
                }

                return "Press E to activate switch";
            }
        }

        private void Awake()
        {
            if (indicatorRenderer == null)
            {
                indicatorRenderer = GetComponent<Renderer>();
            }

            UpdateIndicator();
            clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.spatialBlend = 1f;
            clickSource.minDistance = 1.5f;
            clickSource.maxDistance = 12f;
            clickSource.volume = 0.6f;
        }

        public void Interact(GameObject user)
        {
            if (hasBeenUsed && canOnlyBeUsedOnce)
            {
                return;
            }

            if (linkedDoor == null)
            {
                Debug.LogError(
                    $"{name} does not have a linked door.",
                    this);
                return;
            }

            linkedDoor.Unlock();

            if (openDoorOnUse)
            {
                linkedDoor.Open();
            }

            hasBeenUsed = true;
            UpdateIndicator();
            clickSource.PlayOneShot(ProceduralSfx.Click);
        }

        private void UpdateIndicator()
        {
            if (indicatorRenderer != null)
            {
                indicatorRenderer.material.color =
                    hasBeenUsed ? activatedColour : readyColour;
            }
        }
    }
}
