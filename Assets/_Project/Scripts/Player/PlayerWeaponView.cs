using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Player
{
    /// <summary>Shows the authored rifle in first person without adding colliders to the player.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponView : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject riflePrefab;
        [SerializeField] private Vector3 localPosition = new Vector3(0.34f, -0.32f, 0.66f);
        [SerializeField] private Vector3 localEulerAngles = new Vector3(0f, 90f, 0f);
        [SerializeField, Min(0.01f)] private float visibleLength = 0.75f;

        private GameObject rifle;
        private PlayerHold playerHold;

        private void Awake()
        {
            playerHold = GetComponent<PlayerHold>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null || riflePrefab == null) return;

            var anchor = new GameObject("First Person Rifle").transform;
            anchor.SetParent(playerCamera.transform, false);
            anchor.localPosition = localPosition;
            anchor.localRotation = Quaternion.Euler(localEulerAngles);

            rifle = Instantiate(riflePrefab, anchor);
            rifle.name = "Plasma Rifle View Model";
            rifle.transform.localPosition = Vector3.zero;
            rifle.transform.localRotation = Quaternion.identity;
            foreach (Collider collider in rifle.GetComponentsInChildren<Collider>(true))
                Destroy(collider);

            Renderer[] renderers = rifle.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (largest > 0.001f) rifle.transform.localScale *= visibleLength / largest;
            }

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(playerCamera.transform, false);
            muzzle.localPosition = new Vector3(0.34f, -0.20f, 0.88f);
        }

        private void LateUpdate()
        {
            if (rifle == null || playerCamera == null)
            {
                return;
            }

            bool carrying = playerHold != null && playerHold.IsHoldingObject;
            bool obstructed = !carrying && Physics.SphereCast(
                playerCamera.transform.position,
                0.22f,
                playerCamera.transform.forward,
                out _,
                0.95f,
                ~(1 << GameLayers.Player),
                QueryTriggerInteraction.Ignore);
            bool showRifle = !carrying && !obstructed;
            if (rifle.activeSelf != showRifle)
            {
                rifle.SetActive(showRifle);
            }
        }
    }
}
