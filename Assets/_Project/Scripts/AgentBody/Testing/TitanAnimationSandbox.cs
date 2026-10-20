using System.Collections.Generic;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Body.Testing
{
    /// <summary>
    /// Animation-only sandbox brain: alternates hand-placed Walk/Run routes with Idle
    /// pauses. All translation and turning remain owned by the shared AgentBody.
    /// </summary>
    [RequireComponent(typeof(AgentBody))]
    [DisallowMultipleComponent]
    public sealed class TitanAnimationSandbox : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints = new Transform[0];
        [SerializeField, Min(0)] private float idlePause = 2.5f;
        [SerializeField] private Camera followCamera;
        [SerializeField] private Vector3 cameraOffset = new Vector3(4, 3.2f, -6);

        private AgentBody body;
        private AgentAnimatorDriver animationDriver;
        private readonly List<Vector3> route = new List<Vector3>();
        private bool automatic = true;
        private bool waiting;
        private float resumeAt;
        private MoveStyle nextStyle = MoveStyle.Walk;

        public string Phase { get; private set; } = "Idle";
        public bool Automatic => automatic;

        private void Awake()
        {
            body = GetComponent<AgentBody>();
            animationDriver = GetComponent<AgentAnimatorDriver>();
        }

        private void OnEnable()
        {
            body.Arrived += OnArrived;
            body.Stuck += OnStuck;
            SchedulePause();
        }

        private void OnDisable()
        {
            body.Arrived -= OnArrived;
            body.Stuck -= OnStuck;
            waiting = false;
            body.Stop();
        }

        private void Update()
        {
            if (!automatic || !waiting || Time.time < resumeAt) return;
            waiting = false;
            SendRoute(nextStyle);
        }

        private void LateUpdate()
        {
            if (followCamera == null) return;
            followCamera.transform.position = transform.position + cameraOffset;
            followCamera.transform.LookAt(transform.position + Vector3.up);
        }

        public void WalkRoute() { automatic = false; SendRoute(MoveStyle.Walk); }
        public void RunRoute() { automatic = false; SendRoute(MoveStyle.Run); }

        public void StopDemo()
        {
            automatic = waiting = false;
            body.Stop();
            Phase = "Idle (Stop)";
        }

        public void RestartAutomatic()
        {
            body.Stop();
            automatic = true;
            nextStyle = MoveStyle.Walk;
            SchedulePause();
        }

        private void SendRoute(MoveStyle style)
        {
            waiting = false;
            route.Clear();
            // A Walk/Run button pressed mid-route replaces only its style, not its progress.
            if (body.IsMoving && body.CurrentWaypointIndex < body.Waypoints.Count)
            {
                for (int i = body.CurrentWaypointIndex; i < body.Waypoints.Count; i++) route.Add(body.Waypoints[i]);
                ExecuteRoute(style);
                return;
            }
            if (waypoints.Length < 2)
            {
                StopDemo();
                Phase = "Missing route: assign at least two waypoints";
                return;
            }
            // Start at whichever end is nearer. Replacing a route never teleports the robot.
            if (waypoints[0] == null || waypoints[waypoints.Length - 1] == null)
            {
                StopDemo();
                Phase = "Missing waypoint";
                return;
            }
            bool reverse = (transform.position - waypoints[waypoints.Length - 1].position).sqrMagnitude <
                           (transform.position - waypoints[0].position).sqrMagnitude;
            for (int i = 0; i < waypoints.Length; i++)
            {
                Transform point = waypoints[reverse ? waypoints.Length - 1 - i : i];
                if (point == null) { StopDemo(); Phase = "Missing waypoint"; return; }
                route.Add(point.position);
            }
            ExecuteRoute(style);
        }

        private void ExecuteRoute(MoveStyle style)
        {
            Phase = style.ToString();
            nextStyle = style == MoveStyle.Walk ? MoveStyle.Run : MoveStyle.Walk;
            body.Execute(new MoveCommand(route, style));
        }

        private void OnArrived()
        {
            Phase = "Idle (Arrived)";
            if (automatic) SchedulePause();
        }

        private void OnStuck()
        {
            StopDemo();
            Phase = "Stuck: check sandbox route/colliders; choose Walk or Run to retry";
        }

        private void SchedulePause()
        {
            Phase = "Idle";
            waiting = true;
            resumeAt = Time.time + Mathf.Max(0, idlePause);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(15, 15, 560, 155), GUI.skin.box);
            GUILayout.Label("Titan / AgentBody animation sandbox — " + Phase);
            GUILayout.Label("Automatic: Idle → Walk → Idle → Run. Camera follows the CharacterController root.");
            if (animationDriver != null)
                GUILayout.Label($"Actual {animationDriver.ActualHorizontalSpeed:F2} m/s | Speed {animationDriver.SmoothedSpeedParameter:F2} | {animationDriver.Status}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Walk")) WalkRoute();
            if (GUILayout.Button("Run")) RunRoute();
            if (GUILayout.Button("Stop / Idle")) StopDemo();
            if (GUILayout.Button("Automatic")) RestartAutomatic();
            GUILayout.EndHorizontal();
            GUILayout.Label("In-place clips only. No NavMeshAgent, root motion or AI brain changes.");
            GUILayout.EndArea();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(waypoints[i].position, 0.2f);
                if (i > 0 && waypoints[i - 1] != null) Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
            }
        }
    }
}
