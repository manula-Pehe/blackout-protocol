using UnityEngine;

namespace BlackoutProtocol.Audio
{
    public static class WorldSpaceVisibility
    {
        public static bool CanSee(Camera camera, Transform owner, Vector3 target)
        {
            if (camera == null) return false;
            Vector3 origin = camera.transform.position;
            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            if (distance < 0.2f) return true;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction / distance, distance,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                Transform obstacle = hit.collider.transform;
                if (obstacle.IsChildOf(camera.transform.root) || obstacle.IsChildOf(owner)) continue;
                return false;
            }
            return true;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorldSpaceLabelVisibility : MonoBehaviour
    {
        private Renderer labelRenderer;

        private void Awake() => labelRenderer = GetComponent<Renderer>();

        private void LateUpdate()
        {
            if (labelRenderer == null) return;
            Camera camera = Camera.main;
            labelRenderer.enabled = camera != null &&
                WorldSpaceVisibility.CanSee(camera, transform, transform.position);
        }
    }
}
