using System.Collections.Generic;
using BlackoutProtocol.AI.Breacher.Logic;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.AI.Breacher
{
    /// <summary>
    /// Moves the Breacher along paths produced by the custom A* search.
    /// NavMesh is sampled only for walkable geometry; NavMeshAgent is not used.
    /// </summary>
    public sealed class BreacherPathMover : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float turnSpeed = 540f;
        [SerializeField] private float waypointTolerance = 0.15f;
        [SerializeField] private float destinationTolerance = 0.35f;

        [Header("Pathfinding")]
        [SerializeField] private float navMeshSampleRadius = 2f;
        [SerializeField] private int maximumExpandedNodes = 4096;

        [Header("Debug")]
        [SerializeField] private bool drawPath = true;
        [SerializeField] private Transform testDestination;
        [SerializeField] private bool moveToTestDestinationOnStart;

        private readonly List<Vector3> waypoints = new List<Vector3>();
        private int waypointIndex;

        public bool IsMoving { get; private set; }
        public bool HasReachedDestination { get; private set; }
        public bool LastPathSucceeded { get; private set; }
        public float LastPathCost { get; private set; }

        private void Start()
        {
            if (moveToTestDestinationOnStart && testDestination != null)
            {
                MoveTo(testDestination.position);
            }
        }

        private void Update()
        {
            FollowPath();
        }

        [ContextMenu("Debug/Move To Test Destination")]
        private void MoveToTestDestination()
        {
            if (testDestination == null)
            {
                Debug.LogWarning(
                    "Assign a Test Destination before testing movement.",
                    this);
                return;
            }

            MoveTo(testDestination.position);
        }

        public bool MoveTo(Vector3 destination)
        {
            Stop();
            HasReachedDestination = false;

            if (!TrySamplePosition(transform.position, out Vector3 start) ||
                !TrySamplePosition(destination, out Vector3 goal))
            {
                LastPathSucceeded = false;
                return false;
            }

            NavGraph graph = NavMeshGraphBuilder.Build();

            if (graph.Nodes.Count == 0)
            {
                LastPathSucceeded = false;
                return false;
            }

            NavNode startNode = FindNearestNode(graph.Nodes, start);
            NavNode goalNode = FindNearestNode(graph.Nodes, goal);
            PathSearchResult result = PathSearch.FindWithAStar(
                startNode,
                goalNode,
                maximumExpandedNodes);

            LastPathSucceeded = result.Succeeded;
            LastPathCost = result.TotalCost;

            if (!result.Succeeded)
            {
                Debug.LogWarning(
                    $"Breacher could not find a path to {destination}.",
                    this);
                return false;
            }

            // The first node contains the Breacher, so subsequent nodes are
            // the useful route waypoints. The sampled goal is the final point.
            for (int index = 1; index < result.Path.Count; index++)
            {
                waypoints.Add(result.Path[index].Position);
            }

            waypoints.Add(goal);
            waypointIndex = 0;
            IsMoving = true;

            Debug.Log(
                $"Breacher A*: cost={result.TotalCost:F2}, " +
                $"expanded={result.ExpandedNodeCount}, " +
                $"waypoints={waypoints.Count}",
                this);
            return true;
        }

        public void Stop()
        {
            IsMoving = false;
            waypoints.Clear();
            waypointIndex = 0;
        }

        private void FollowPath()
        {
            if (!IsMoving || waypointIndex >= waypoints.Count)
            {
                return;
            }

            Vector3 target = waypoints[waypointIndex];
            Vector3 offset = target - transform.position;
            offset.y = 0f;

            float tolerance = waypointIndex == waypoints.Count - 1
                ? destinationTolerance
                : waypointTolerance;

            if (offset.sqrMagnitude <= tolerance * tolerance)
            {
                waypointIndex++;

                if (waypointIndex >= waypoints.Count)
                {
                    IsMoving = false;
                    HasReachedDestination = true;
                }

                return;
            }

            Vector3 direction = offset.normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);
        }

        private bool TrySamplePosition(Vector3 position, out Vector3 sampled)
        {
            if (NavMesh.SamplePosition(
                    position,
                    out NavMeshHit hit,
                    navMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                sampled = hit.position;
                return true;
            }

            sampled = position;
            return false;
        }

        private static NavNode FindNearestNode(
            IReadOnlyList<NavNode> nodes,
            Vector3 position)
        {
            NavNode nearest = nodes[0];
            float nearestDistance =
                (nearest.Position - position).sqrMagnitude;

            for (int index = 1; index < nodes.Count; index++)
            {
                float distance =
                    (nodes[index].Position - position).sqrMagnitude;

                if (distance < nearestDistance)
                {
                    nearest = nodes[index];
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void OnDrawGizmos()
        {
            if (!drawPath || waypoints.Count == 0)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Vector3 previous = transform.position;

            for (int index = waypointIndex; index < waypoints.Count; index++)
            {
                Vector3 waypoint = waypoints[index];
                Gizmos.DrawLine(previous, waypoint);
                Gizmos.DrawSphere(waypoint, 0.08f);
                previous = waypoint;
            }
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            turnSpeed = Mathf.Max(1f, turnSpeed);
            waypointTolerance = Mathf.Max(0.01f, waypointTolerance);
            destinationTolerance = Mathf.Max(0.01f, destinationTolerance);
            navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
            maximumExpandedNodes = Mathf.Max(1, maximumExpandedNodes);
        }
    }
}
