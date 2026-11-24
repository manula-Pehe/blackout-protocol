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
        private Transform lever;

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
            BuildVisual();
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
            if (lever != null) lever.localRotation = Quaternion.Euler(35f, 0, 0);
            clickSource.PlayOneShot(ProceduralSfx.Click);
        }

        private void BuildVisual()
        {
            Renderer housing = GetComponent<Renderer>();
            if (housing != null) housing.material.color = new Color(0.15f, 0.18f, 0.21f);
            MakePart("Inset Panel", new Vector3(0, 0, -0.56f),
                new Vector3(0.78f, 0.82f, 0.12f), new Color(0.035f, 0.05f, 0.07f));
            lever = MakePart("Activation Lever", new Vector3(0, -0.08f, -0.78f),
                new Vector3(0.16f, 0.58f, 0.2f), new Color(0.75f, 0.8f, 0.82f)).transform;
            lever.localRotation = Quaternion.Euler(-35f, 0, 0);
            GameObject lamp = MakePart("Status Lamp", new Vector3(0, 0.35f, -0.73f),
                new Vector3(0.28f, 0.14f, 0.12f), readyColour);
            indicatorRenderer = lamp.GetComponent<Renderer>();
        }

        private GameObject MakePart(string partName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider partCollider = part.GetComponent<Collider>();
            partCollider.enabled = false;
            Destroy(partCollider);
            Renderer partRenderer = part.GetComponent<Renderer>();
            Renderer housing = GetComponent<Renderer>();
            if (housing != null) partRenderer.sharedMaterial = housing.sharedMaterial;
            partRenderer.material.color = color;
            return part;
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
