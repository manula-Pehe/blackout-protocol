using BlackoutProtocol.AI.Breacher;
using BlackoutProtocol.AI.Flanker;
using BlackoutProtocol.AI.Hunter;
using BlackoutProtocol.AI.Sentinel;
using UnityEngine;

namespace BlackoutProtocol.Audio
{
    /// <summary>Depth-tested, camera-facing health bar for every scene enemy.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthDisplay : MonoBehaviour
    {
        private HunterHealth hunter;
        private FlankerHealth flanker;
        private BreacherHealth breacher;
        private SentinelDroneBody sentinel;
        private Transform bar;
        private Transform fill;
        private TextMesh label;
        private Material backgroundMaterial;
        private Material fillMaterial;
        private float lastFraction = -1f;

        private void Awake()
        {
            hunter = GetComponent<HunterHealth>();
            flanker = GetComponent<FlankerHealth>();
            breacher = GetComponent<BreacherHealth>();
            sentinel = GetComponent<SentinelDroneBody>();
            if (hunter == null && flanker == null && breacher == null && sentinel == null)
            {
                enabled = false;
                return;
            }

            var container = new GameObject("Health Display");
            bar = container.transform;
            bar.SetParent(transform, false);
            bar.localPosition = Vector3.up * (sentinel != null ? 0.9f : 2.35f);
            backgroundMaterial = MakeMaterial(new Color(0.04f, 0.06f, 0.08f));
            fillMaterial = MakeMaterial(hunter != null ? new Color(0.95f, 0.17f, 0.13f) :
                flanker != null ? new Color(0.12f, 0.48f, 1f) :
                breacher != null ? new Color(1f, 0.55f, 0.1f) : new Color(0.16f, 0.92f, 0.89f));
            MakeQuad("Background", new Vector3(0, 0, 0), new Vector3(1.04f, 0.14f, 1), backgroundMaterial);
            fill = MakeQuad("Health", new Vector3(0, 0, -0.003f), new Vector3(1f, 0.10f, 1), fillMaterial).transform;

            var textObject = new GameObject("Health Percent");
            textObject.transform.SetParent(bar, false);
            textObject.transform.localPosition = new Vector3(0, 0.13f, -0.005f);
            label = textObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.07f;
            label.fontSize = 48;
            label.color = Color.white;
        }

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader);
            material.color = color;
            return material;
        }

        private GameObject MakeQuad(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(bar, false);
            quad.transform.localPosition = position;
            quad.transform.localScale = scale;
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            return quad;
        }

        private void LateUpdate()
        {
            if (bar == null) return;
            Camera camera = Camera.main;
            bool visible = false;
            if (camera != null)
            {
                Vector3 toCamera = camera.transform.position - bar.position;
                if (toCamera.sqrMagnitude > 0.001f) bar.rotation = Quaternion.LookRotation(toCamera);
                Vector3 sightTarget = sentinel != null ? transform.position :
                    transform.position + Vector3.up * 1.1f;
                Vector3 viewport = camera.WorldToViewportPoint(bar.position);
                Vector3 barLeft = bar.position - bar.right * 0.55f;
                Vector3 barRight = bar.position + bar.right * 0.55f;
                visible = viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                    viewport.y >= 0f && viewport.y <= 1f &&
                    WorldSpaceVisibility.CanSee(camera, transform, sightTarget) &&
                    WorldSpaceVisibility.CanSee(camera, transform, bar.position) &&
                    WorldSpaceVisibility.CanSee(camera, transform, barLeft) &&
                    WorldSpaceVisibility.CanSee(camera, transform, barRight) &&
                    WorldSpaceVisibility.CanSee(camera, transform,
                        bar.position + Vector3.up * 0.15f);
            }
            float fraction = hunter != null ? hunter.HealthNormalized : flanker != null ? flanker.HealthNormalized :
                breacher != null ? breacher.CurrentHealth / Mathf.Max(1f, breacher.MaximumHealth) :
                sentinel != null ? sentinel.CurrentHealth / Mathf.Max(1f, sentinel.MaximumHealth) : 0f;
            fraction = Mathf.Clamp01(fraction);
            bool show = fraction > 0f && visible;
            if (bar.gameObject.activeSelf != show) bar.gameObject.SetActive(show);
            if (Mathf.Approximately(fraction, lastFraction)) return;
            lastFraction = fraction;
            fill.localScale = new Vector3(fraction, 0.10f, 1);
            fill.localPosition = new Vector3((fraction - 1f) * 0.5f, 0, -0.003f);
            label.text = Mathf.CeilToInt(fraction * 100f) + "%";
        }

        private void OnDestroy()
        {
            if (backgroundMaterial != null) Destroy(backgroundMaterial);
            if (fillMaterial != null) Destroy(fillMaterial);
        }
    }
}
