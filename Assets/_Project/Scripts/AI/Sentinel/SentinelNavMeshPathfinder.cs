using System;
using System.Collections.Generic;
using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.AI.Sentinel
{
    /// <summary>Measurements from one search over the same Sentinel NavMesh graph.</summary>
    public readonly struct SentinelPathSearchStats
    {
        public SentinelPathSearchStats(bool succeeded, float totalCost, int nodesExpanded, double elapsedMilliseconds)
        {
            Succeeded = succeeded;
            TotalCost = totalCost;
            NodesExpanded = nodesExpanded;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public bool Succeeded { get; }
        public float TotalCost { get; }
        public int NodesExpanded { get; }
        public double ElapsedMilliseconds { get; }
    }

    /// <summary>
    /// Builds a Sentinel-owned graph from baked NavMesh triangles and searches it
    /// with A*. The graph is independent of the Breacher's navigation data.
    /// </summary>
    public sealed class SentinelNavMeshPathfinder : IPathfinder
    {
        private const float VertexPrecision = 1000f;
        private readonly List<Node> nodes = new List<Node>();
        private readonly List<int> reversePath = new List<int>();
        private SentinelGraphSearch graphSearch;
        private bool built;
        private int lastStartNode = -1;
        private int lastGoalNode = -1;
        private bool hasComparableQuery;
        private readonly List<Vector3> lastAStarPath = new List<Vector3>(32);
        private readonly List<Vector3> lastUcsPath = new List<Vector3>(32);

        public SentinelPathSearchStats LastAStar { get; private set; }
        public SentinelPathSearchStats LastUniformCost { get; private set; }
        public bool CanCompareLastSearch => hasComparableQuery;
        public IReadOnlyList<Vector3> LastAStarPath => lastAStarPath;
        public IReadOnlyList<Vector3> LastUniformCostPath => lastUcsPath;
        public int GraphNodeCount => nodes.Count;
        public string LastQueryStatus { get; private set; } = "No route query yet.";

        /// <summary>Rebuilds the route graph from the current baked NavMesh.</summary>
        public void Rebuild()
        {
            nodes.Clear();
            graphSearch = null;
            built = true;
            hasComparableQuery = false;
            lastAStarPath.Clear();
            lastUcsPath.Clear();
            LastAStar = LastUniformCost = default;
            LastQueryStatus = "Graph rebuilt; waiting for a route query.";

            NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
            int triangleCount = mesh.indices.Length / 3;
            if (triangleCount == 0)
            {
                return;
            }

            var edges = new Dictionary<EdgeKey, int>(triangleCount * 2);
            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                int offset = triangle * 3;
                Vector3 a = mesh.vertices[mesh.indices[offset]];
                Vector3 b = mesh.vertices[mesh.indices[offset + 1]];
                Vector3 c = mesh.vertices[mesh.indices[offset + 2]];
                nodes.Add(new Node((a + b + c) / 3f));
            }

            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                int offset = triangle * 3;
                AddEdge(mesh.vertices[mesh.indices[offset]], mesh.vertices[mesh.indices[offset + 1]], triangle, edges);
                AddEdge(mesh.vertices[mesh.indices[offset + 1]], mesh.vertices[mesh.indices[offset + 2]], triangle, edges);
                AddEdge(mesh.vertices[mesh.indices[offset + 2]], mesh.vertices[mesh.indices[offset]], triangle, edges);
            }

            var positions = new Vector3[nodes.Count];
            var graphEdges = new SentinelGraphEdge[nodes.Count][];
            for (int i = 0; i < nodes.Count; i++)
            {
                positions[i] = nodes[i].Position;
                List<Connection> connections = nodes[i].Connections;
                graphEdges[i] = new SentinelGraphEdge[connections.Count];
                for (int j = 0; j < connections.Count; j++)
                    graphEdges[i][j] = new SentinelGraphEdge(connections[j].Target, connections[j].Cost);
            }
            graphSearch = new SentinelGraphSearch(positions, graphEdges);
        }

        /// <inheritdoc />
        public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Clear();
            hasComparableQuery = false;
            lastStartNode = lastGoalNode = -1;
            lastAStarPath.Clear();
            lastUcsPath.Clear();
            LastAStar = default;
            LastUniformCost = default;
            if (!built)
            {
                Rebuild();
            }

            if (!TrySample(start, out Vector3 sampledStart) ||
                !TrySample(goal, out Vector3 sampledGoal))
            {
                LastQueryStatus = "Start or destination is outside the NavMesh sample radius.";
                return false;
            }

            if (nodes.Count == 0)
            {
                LastQueryStatus = "The baked NavMesh graph is empty.";
                return false;
            }

            int startNode = FindNearestNode(sampledStart);
            int goalNode = FindNearestNode(sampledGoal);
            if (startNode < 0 || goalNode < 0)
            {
                LastQueryStatus = "Could not find graph anchors for the route endpoints.";
                return false;
            }

            if (startNode == goalNode || !NavMesh.Raycast(sampledStart, sampledGoal, out _, NavMesh.AllAreas))
            {
                LastQueryStatus = "Direct NavMesh route; graph comparison is not needed for this query.";
                result.Add(sampledStart);
                if ((sampledGoal - sampledStart).sqrMagnitude > 0.01f)
                {
                    result.Add(sampledGoal);
                }
                return true;
            }

            lastStartNode = startNode;
            lastGoalNode = goalNode;
            hasComparableQuery = true;
            LastAStar = Search(startNode, goalNode, useHeuristic: true, reversePath);
            CopyNodePath(reversePath, lastAStarPath);
            if (!LastAStar.Succeeded)
            {
                LastQueryStatus = "A* could not reach the goal or reached its expansion limit.";
                return false;
            }

            // Pull the route tight while NavMesh.Raycast confirms every shortcut is walkable.
            result.Add(sampledStart);
            Vector3 from = sampledStart;
            int pathIndex = 0;
            while (pathIndex < reversePath.Count)
            {
                int furthest = pathIndex;
                Vector3 furthestPoint = nodes[reversePath[pathIndex]].Position;
                for (int candidate = pathIndex + 1; candidate < reversePath.Count; candidate++)
                {
                    Vector3 candidatePoint = nodes[reversePath[candidate]].Position;
                    if (NavMesh.Raycast(from, candidatePoint, out _, NavMesh.AllAreas))
                    {
                        break;
                    }
                    furthest = candidate;
                    furthestPoint = candidatePoint;
                }

                if ((furthestPoint - result[result.Count - 1]).sqrMagnitude > 0.01f)
                {
                    result.Add(furthestPoint);
                }
                from = furthestPoint;
                pathIndex = furthest + 1;
            }

            if ((sampledGoal - result[result.Count - 1]).sqrMagnitude > 0.01f)
            {
                result.Add(sampledGoal);
            }
            LastQueryStatus = "A* route found; comparison is available in the Sentinel debug view.";
            return true;
        }

        /// <summary>Runs UCS (the same graph search with h = 0) for the most recent A* query.</summary>
        public bool TryCompareLastSearch(out SentinelPathSearchStats aStar, out SentinelPathSearchStats uniformCost)
        {
            if (!hasComparableQuery)
            {
                aStar = LastAStar;
                uniformCost = LastUniformCost;
                LastQueryStatus = "No graph-based A* query is available to compare yet.";
                return false;
            }

            return RunComparison(lastStartNode, lastGoalNode, out aStar, out uniformCost);
        }

        /// <summary>Runs both algorithms on graph anchors for the supplied endpoints, even if movement can take a direct shortcut.</summary>
        public bool TryCompareSearches(Vector3 start, Vector3 goal,
            out SentinelPathSearchStats aStar, out SentinelPathSearchStats uniformCost)
        {
            hasComparableQuery = false;
            lastStartNode = lastGoalNode = -1;
            lastAStarPath.Clear();
            lastUcsPath.Clear();
            LastAStar = LastUniformCost = default;
            if (!built) Rebuild();
            if (nodes.Count == 0 || !TrySample(start, out Vector3 sampledStart) ||
                !TrySample(goal, out Vector3 sampledGoal))
            {
                aStar = LastAStar;
                uniformCost = LastUniformCost;
                LastQueryStatus = "Cannot compare: NavMesh graph or sampled endpoints are unavailable.";
                return false;
            }

            int startNode = FindNearestNode(sampledStart);
            int goalNode = FindNearestNode(sampledGoal);
            if (startNode < 0 || goalNode < 0)
            {
                aStar = LastAStar;
                uniformCost = LastUniformCost;
                LastQueryStatus = "Cannot compare: graph anchors are unavailable.";
                return false;
            }

            lastStartNode = startNode;
            lastGoalNode = goalNode;
            hasComparableQuery = true;
            return RunComparison(startNode, goalNode, out aStar, out uniformCost);
        }

        private bool RunComparison(int startNode, int goalNode,
            out SentinelPathSearchStats aStar, out SentinelPathSearchStats uniformCost)
        {
            LastAStar = Search(startNode, goalNode, useHeuristic: true, reversePath);
            CopyNodePath(reversePath, lastAStarPath);
            LastUniformCost = Search(startNode, goalNode, useHeuristic: false, reversePath);
            CopyNodePath(reversePath, lastUcsPath);

            aStar = LastAStar;
            uniformCost = LastUniformCost;
            LastQueryStatus = aStar.Succeeded && uniformCost.Succeeded
                ? "A* and UCS completed on the same graph query. Costs omit endpoint-to-anchor legs."
                : "A* or UCS could not reach the goal or reached its expansion limit.";
            return aStar.Succeeded && uniformCost.Succeeded;
        }

        private SentinelPathSearchStats Search(int startNode, int goalNode, bool useHeuristic, List<int> path) =>
            graphSearch.Search(startNode, goalNode, useHeuristic, path);

        private void CopyNodePath(List<int> source, List<Vector3> destination)
        {
            destination.Clear();
            for (int i = 0; i < source.Count; i++) destination.Add(nodes[source[i]].Position);
        }

        private int FindNearestNode(Vector3 position)
        {
            int nearest = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < nodes.Count; i++)
            {
                float distance = (nodes[i].Position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = i;
                }
            }
            return nearest;
        }

        private static bool TrySample(Vector3 position, out Vector3 sampled)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                sampled = hit.position;
                return true;
            }
            sampled = default;
            return false;
        }

        private void AddEdge(Vector3 first, Vector3 second, int triangle, Dictionary<EdgeKey, int> owners)
        {
            var key = new EdgeKey(first, second);
            if (!owners.TryGetValue(key, out int neighbour))
            {
                owners.Add(key, triangle);
                return;
            }

            float cost = Vector3.Distance(nodes[triangle].Position, nodes[neighbour].Position);
            nodes[triangle].Connections.Add(new Connection(neighbour, cost));
            nodes[neighbour].Connections.Add(new Connection(triangle, cost));
        }

        private sealed class Node
        {
            public Node(Vector3 position) => Position = position;
            public Vector3 Position { get; }
            public List<Connection> Connections { get; } = new List<Connection>(3);
        }

        private readonly struct Connection
        {
            public Connection(int target, float cost) { Target = target; Cost = cost; }
            public int Target { get; }
            public float Cost { get; }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly int ax, ay, az, bx, by, bz;

            public EdgeKey(Vector3 a, Vector3 b)
            {
                int aX = Mathf.RoundToInt(a.x * VertexPrecision);
                int aY = Mathf.RoundToInt(a.y * VertexPrecision);
                int aZ = Mathf.RoundToInt(a.z * VertexPrecision);
                int bX = Mathf.RoundToInt(b.x * VertexPrecision);
                int bY = Mathf.RoundToInt(b.y * VertexPrecision);
                int bZ = Mathf.RoundToInt(b.z * VertexPrecision);
                bool ordered = aX < bX || (aX == bX && (aY < bY || (aY == bY && aZ <= bZ)));
                ax = ordered ? aX : bX; ay = ordered ? aY : bY; az = ordered ? aZ : bZ;
                bx = ordered ? bX : aX; by = ordered ? bY : aY; bz = ordered ? bZ : aZ;
            }

            public bool Equals(EdgeKey other) => ax == other.ax && ay == other.ay && az == other.az && bx == other.bx && by == other.by && bz == other.bz;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ax;
                    hash = hash * 397 ^ ay; hash = hash * 397 ^ az;
                    hash = hash * 397 ^ bx; hash = hash * 397 ^ by;
                    return hash * 397 ^ bz;
                }
            }
        }
    }
}
