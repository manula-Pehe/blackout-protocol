using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.AI.Hunter.Testing
{
    /// <summary>Manual shared-event sources. Buttons emit noises; they never command the brain or move the Hunter.</summary>
    public sealed class HunterSandboxNoiseTrigger : MonoBehaviour
    {
        [SerializeField] private HunterBrain hunter;
        [SerializeField] private Transform acrossWall;
        [SerializeField] private HunterSandboxTarget testTarget;
        [SerializeField] private bool showControls = true;
        private string lastEmission = "Silence: watch the Hunter wander. Click an alarm to investigate across the wall.";

        public HunterSandboxTarget TestTarget => testTarget;
        public string LastEmission => lastEmission;

        [ContextMenu("Emit alarm across wall")]
        public void EmitAlarmAcrossWall()
        {
            if (acrossWall != null) Emit(acrossWall.position, NoiseType.Alarm, NoiseLevels.Alarm);
        }

        [ContextMenu("Emit nearby gunshot")]
        public void EmitNearbyGunshot()
        {
            if (TryNearbyPoint(out Vector3 point)) Emit(point, NoiseType.Gunshot, NoiseLevels.Gunshot);
        }

        public void EmitRepeatedImpacts()
        {
            if (!TryNearbyPoint(out Vector3 point)) return;
            for (int i = 0; i < 5; i++) Emit(point, NoiseType.Impact, NoiseLevels.Impact);
        }

        public void EmitSingleImpact()
        {
            if (TryNearbyPoint(out Vector3 point)) Emit(point, NoiseType.Impact, NoiseLevels.Impact);
        }

        public void ShowTarget()
        {
            if (testTarget == null || !TryNearbyPoint(out Vector3 point)) return;
            testTarget.gameObject.SetActive(false);
            testTarget.transform.position = point;
            testTarget.gameObject.SetActive(true);
            Physics.SyncTransforms();
            lastEmission = "Registered player stand-in shown nearby. Hunter must see it and enter attack range.";
        }

        public void HideTarget()
        {
            if (testTarget != null) testTarget.gameObject.SetActive(false);
        }

        private bool TryNearbyPoint(out Vector3 point)
        {
            point = default;
            if (hunter == null) return false;
            // Try several visible floor positions so a button cannot spawn a player through the wall.
            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = Quaternion.Euler(0, i * 45, 0) * hunter.transform.forward * 3;
                if (!NavMesh.SamplePosition(hunter.transform.position + offset, out NavMeshHit hit, 1, NavMesh.AllAreas)) continue;
                if (NavMesh.Raycast(hunter.transform.position, hit.position, out _, NavMesh.AllAreas)) continue;
                point = hit.position;
                return true;
            }
            return false;
        }

        private void Emit(Vector3 position, NoiseType category, float radius)
        {
            if (!Application.isPlaying) return;
            GameEvents.RaiseNoise(new NoiseEvent(position, radius, category, Time.time, gameObject));
            lastEmission = $"Emitted {category} at {position}, audible radius {radius:F0} m. No further sound is generated automatically.";
        }

        private void OnGUI()
        {
            if (!showControls) return;
            GUILayout.BeginArea(new Rect(16, Screen.height - 165, Mathf.Min(Screen.width - 32, 1000), 150), GUI.skin.box);
            GUILayout.Label("HUNTER STAGE B — shared noise events and a stationary player stand-in");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Alarm across wall")) EmitAlarmAcrossWall();
            if (GUILayout.Button("Gunshot nearby")) EmitNearbyGunshot();
            if (GUILayout.Button("Repeated impacts")) EmitRepeatedImpacts();
            if (GUILayout.Button("Show target")) ShowTarget();
            if (GUILayout.Button("Hide target")) HideTarget();
            GUILayout.EndHorizontal();
            GUILayout.Label(lastEmission);
            GUILayout.Label("Let the trail cool: Investigate/Chase → Search → Wander. Stage A remains in HunterSandbox.unity.");
            GUILayout.EndArea();
        }
    }
}
