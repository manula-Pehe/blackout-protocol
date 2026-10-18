using System.Collections.Generic;
using BlackoutProtocol.AI.Breacher.Logic;
using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.AI.Breacher
{
    /// <summary>
    /// Moves the Breacher along paths produced by the custom A* search.
    /// NavMesh is sampled only for walkable geometry; NavMeshAgent is not used.
    /// </summary>
    public sealed class BreacherPathMover : MonoBehaviour, IPathfinder
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float turnSpeed = 540f;
        [SerializeField] private float waypointTolerance = 0.15f;
        [SerializeField] private float destinationTolerance = 0.35f;

        [Header("Pathfinding")]
        [SerializeField] private float navMeshSampleRadius = 2f;
        [SerializeField] private int maximumExpandedNodes = 4096;
        [SerializeField] private bool smoothPath = true;

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

        /// <inheritdoc />
        public bool TryFindPath(
            Vector3 startPosition,
            Vector3 goalPosition,
            List<Vector3> result)
        {
            if (result == null)
            {
                throw new System.ArgumentNullException(nameof(result));
            }

            result.Clear();

            if (!TrySamplePosition(startPosition, out Vector3 start) ||
                !TrySamplePosition(goalPosition, out Vector3 goal))
            {
                return false;
            }

            NavGraph graph = NavMeshGraphBuilder.Build();

            if (graph.Nodes.Count == 0)
            {
                return false;
            }

            NavNode startNode = FindNearestNode(graph.Nodes, start);
            NavNode goalNode = FindNearestNode(graph.Nodes, goal);
            PathSearchResult search = PathSearch.FindWithAStar(
                startNode,
                goalNode,
                maximumExpandedNodes);

            if (!search.Succeeded)
            {
                return false;
            }

            var rawWaypoints = new List<Vector3>();

            for (int index = 1; index < search.Path.Count; index++)
            {
                rawWaypoints.Add(search.Path[index].Position);
            }

            rawWaypoints.Add(goal);
            result.Add(start);

            if (smoothPath)
            {
                BuildSmoothedPath(start, rawWaypoints, result);
            }
            else
            {
                result.AddRange(rawWaypoints);
            }

            return result.Count > 1;
        }

        /// <summary>
        /// Calculates a route cost without starting movement. GOAP uses this
        /// to compare destinations using the same custom A* as execution.
        /// </summary>
        public bool TryCalculatePathCost(
            Vector3 destination,
            out float pathCost)
        {
            pathCost = float.PositiveInfinity;

            if (!TrySamplePosition(transform.position, out Vector3 start) ||
                !TrySamplePosition(destination, out Vector3 goal))
            {
                return false;
            }

            NavGraph graph = NavMeshGraphBuilder.Build();

            if (graph.Nodes.Count == 0)
            {
                return false;
            }

            NavNode startNode = FindNearestNode(graph.Nodes, start);
            NavNode goalNode = FindNearestNode(graph.Nodes, goal);
            PathSearchResult result = PathSearch.FindWithAStar(
                startNode,
                goalNode,
                maximumExpandedNodes);

            if (!result.Succeeded)
            {
                return false;
            }

            var rawWaypoints = new List<Vector3>();

            for (int index = 1; index < result.Path.Count; index++)
            {
                rawWaypoints.Add(result.Path[index].Position);
            }

            rawWaypoints.Add(goal);

            var costWaypoints = new List<Vector3>();

            if (smoothPath)
            {
                BuildSmoothedPath(start, rawWaypoints, costWaypoints);
            }
            else
            {
                costWaypoints.AddRange(rawWaypoints);
            }

            pathCost = CalculateWaypointLength(start, costWaypoints);
            return true;
        }

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

            var rawWaypoints = new List<Vector3>();

            // The first node contains the Breacher, so subsequent nodes are
            // the useful route waypoints. The sampled goal is the final point.
            for (int index = 1; index < result.Path.Count; index++)
            {
                rawWaypoints.Add(result.Path[index].Position);
            }

            rawWaypoints.Add(goal);

            if (smoothPath)
            {
                BuildSmoothedPath(start, rawWaypoints, waypoints);
            }
            else
            {
                waypoints.AddRange(rawWaypoints);
            }

            waypointIndex = 0;
            IsMoving = true;

            Debug.Log(
                $"Breacher A*: cost={result.TotalCost:F2}, " +
                $"expanded={result.ExpandedNodeCount}, " +
                $"graphNodes={result.Path.Count}, " +
                $"smoothedWaypoints={waypoints.Count}",
                this);
            return true;
        }

        private void BuildSmoothedPath(
            Vector3 start,
            IReadOnlyList<Vector3> rawWaypoints,
            ICollection<Vector3> output)
        {
            Vector3 anchor = start;
            int nextIndex = 0;

            while (nextIndex < rawWaypoints.Count)
            {
                int furthestVisibleIndex = nextIndex;

                // Pick the furthest later waypoint that can be reached by a
                // straight walkable segment without crossing a NavMesh edge.
                for (int candidate = rawWaypoints.Count - 1;
                     candidate > nextIndex;
                     candidate--)
                {
                    if (!NavMesh.Raycast(
                            anchor,
                            rawWaypoints[candidate],
                            out _,
                            NavMesh.AllAreas))
                    {
                        furthestVisibleIndex = candidate;
                        break;
                    }
                }

                Vector3 selected = rawWaypoints[furthestVisibleIndex];
                output.Add(selected);
                anchor = selected;
                nextIndex = furthestVisibleIndex + 1;
            }
        }

        private static float CalculateWaypointLength(
            Vector3 start,
            IReadOnlyList<Vector3> path)
        {
            float total = 0f;
            Vector3 previous = start;

            for (int index = 0; index < path.Count; index++)
            {
                total += Vector3.Distance(previous, path[index]);
                previous = path[index];
            }

            return Mathf.Max(0.1f, total);
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
