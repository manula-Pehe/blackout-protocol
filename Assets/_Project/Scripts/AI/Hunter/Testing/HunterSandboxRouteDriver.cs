using System.Collections.Generic;
using BlackoutProtocol.Body;
using BlackoutProtocol.Core;
using Unity.AI.Navigation;
using UnityEngine;

namespace BlackoutProtocol.AI.Hunter.Testing
{
    /// <summary>One sandbox route: Hunter A* plans, the shared body executes.</summary>
    [DisallowMultipleComponent]
    public sealed class HunterSandboxRouteDriver : MonoBehaviour
    {
        public enum RouteState { Idle, Following, Arrived, Failed }

        [SerializeField] private AgentBody body;
        [SerializeField] private HunterNavMeshPathfinder pathfinder;
        [SerializeField] private NavMeshSurface navigationSurface;
        [SerializeField] private Transform destination;
        [SerializeField] private LineRenderer pathDisplay;
        [SerializeField] private MoveStyle style = MoveStyle.Walk;
        [SerializeField] private bool playOnStart = true;

        private readonly List<Vector3> waypoints = new List<Vector3>(64);
        private AgentBody subscribedBody;
        private string details = "Press Play to calculate a Hunter A* route.";

        public RouteState State { get; private set; }
        public string Details => details;
        public IReadOnlyList<Vector3> Waypoints => waypoints;
        public Transform Destination => destination;

        private void OnEnable()
        {
            if (TryGetComponent(out HunterBrain brain) && brain.isActiveAndEnabled) enabled = false;
        }

        private void Start()
        {
            if (playOnStart) BeginRoute();
        }

        [ContextMenu("Calculate and follow sandbox route")]
        public bool BeginRoute()
        {
            if (TryGetComponent(out HunterBrain brain) && brain.isActiveAndEnabled)
            {
                enabled = false;
                return false;
            }
            if (!Application.isPlaying)
                return Fail("Enter Play Mode before starting the sandbox route.");

            if (body == null) body = GetComponent<AgentBody>();
            if (pathfinder == null) pathfinder = GetComponent<HunterNavMeshPathfinder>();
            if (body == null || pathfinder == null || destination == null || navigationSurface == null)
                return Fail("Missing component references. Assign AgentBody, HunterNavMeshPathfinder, Destination and Navigation Surface on the route driver.");
            if (!body.isActiveAndEnabled || !pathfinder.isActiveAndEnabled ||
                !TryGetComponent(out CharacterController controller) || !controller.enabled ||
                !TryGetComponent(out AgentMotor motor) || !motor.isActiveAndEnabled ||
                !TryGetComponent(out AgentRotation rotation) || !rotation.isActiveAndEnabled)
                return Fail("Enable the Hunter's CharacterController, AgentBody, AgentMotor, AgentRotation and pathfinder before starting.");
            if (!navigationSurface.isActiveAndEnabled || navigationSurface.navMeshData == null)
                return Fail("Missing baked NavMesh data. Open HunterSandbox and use Tools > Hunter > Bake Open Hunter Sandbox, then save the scene and its NavMesh asset.");
            if (style == MoveStyle.Idle)
                return Fail("Choose a moving style such as Walk; Idle cannot follow the route.");

            Unsubscribe();
            subscribedBody = body;
            subscribedBody.Arrived += OnArrived;
            subscribedBody.Stuck += OnStuck;
            if (!pathfinder.TryFindPath(body.Position, destination.position, waypoints))
                return Fail("Hunter A* could not calculate a route. Check that start and destination are on the same connected baked NavMesh; rebake after geometry changes.");

            if (pathDisplay != null)
            {
                pathDisplay.positionCount = waypoints.Count;
                for (int i = 0; i < waypoints.Count; i++)
                    pathDisplay.SetPosition(i, waypoints[i] + Vector3.up * 0.08f);
            }
            State = RouteState.Following;
            var search = pathfinder.LastSearch;
            details = $"Following Hunter A*: {waypoints.Count} waypoints | graph cost {search.TotalCost:F2} m | {search.NodesExpanded} expanded | {search.ElapsedMilliseconds:F3} ms";
            Debug.Log($"Hunter sandbox: {details}", this);
            IAgentBody agentBody = body;
            agentBody.Execute(new MoveCommand(waypoints, style));
            return true;
        }

        private bool Fail(string reason)
        {
            if (body != null && State == RouteState.Following) body.Stop();
            Unsubscribe();
            State = RouteState.Failed;
            details = reason;
            waypoints.Clear();
            if (pathDisplay != null) pathDisplay.positionCount = 0;
            Debug.LogError($"Hunter sandbox: {reason}", this);
            return false;
        }

        private void OnArrived()
        {
            State = RouteState.Arrived;
            details = "Arrived at the green goal using Hunter A* and the shared AgentBody.";
            Debug.Log($"Hunter sandbox: {details}", this);
        }

        private void OnStuck() => Fail("AgentBody reported Stuck. Check obstacle clearance and waypoint acceptance radius, then rebake if geometry changed.");

        private void OnDisable()
        {
            if (body != null && State == RouteState.Following) body.Stop();
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (subscribedBody == null) return;
            subscribedBody.Arrived -= OnArrived;
            subscribedBody.Stuck -= OnStuck;
            subscribedBody = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 point = waypoints[i] + Vector3.up * 0.12f;
                Gizmos.DrawSphere(point, 0.1f);
                if (i > 0) Gizmos.DrawLine(waypoints[i - 1] + Vector3.up * 0.12f, point);
            }
            if (destination == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(destination.position + Vector3.up * 0.15f, 0.4f);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(16, 16, Mathf.Min(Screen.width - 32, 850), 86), GUIContent.none);
            GUI.Label(new Rect(30, 25, Screen.width - 60, 24), $"BLACKOUT PROTOCOL  /  HUNTER NAVIGATION SANDBOX  /  {State}");
            GUI.Label(new Rect(30, 49, Screen.width - 60, 44), details);
        }
    }
}
