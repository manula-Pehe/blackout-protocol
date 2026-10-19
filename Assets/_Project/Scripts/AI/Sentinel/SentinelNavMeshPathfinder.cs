using System;
using System.Collections.Generic;
using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.AI.Sentinel
{
    /// <summary>
    /// Builds a Sentinel-owned graph from baked NavMesh triangles and searches it
    /// with A*. The graph is independent of the Breacher's navigation data.
    /// </summary>
    public sealed class SentinelNavMeshPathfinder : IPathfinder
    {
        private const float VertexPrecision = 1000f;
        private readonly List<Node> nodes = new List<Node>();
        private readonly List<int> open = new List<int>();
        private readonly List<int> reversePath = new List<int>();
        private float[] costs = Array.Empty<float>();
        private int[] previous = Array.Empty<int>();
        private bool[] closed = Array.Empty<bool>();
        private bool built;

        /// <summary>Rebuilds the route graph from the current baked NavMesh.</summary>
        public void Rebuild()
        {
            nodes.Clear();
            built = true;

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

            costs = new float[nodes.Count];
            previous = new int[nodes.Count];
            closed = new bool[nodes.Count];
        }

        /// <inheritdoc />
        public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Clear();
            if (!built)
            {
                Rebuild();
            }

            if (!TrySample(start, out Vector3 sampledStart) ||
                !TrySample(goal, out Vector3 sampledGoal))
            {
                return false;
            }

            if (nodes.Count == 0)
            {
                return false;
            }

            int startNode = FindNearestNode(sampledStart);
            int goalNode = FindNearestNode(sampledGoal);
            if (startNode < 0 || goalNode < 0)
            {
                return false;
            }

            if (startNode == goalNode || !NavMesh.Raycast(sampledStart, sampledGoal, out _, NavMesh.AllAreas))
            {
                result.Add(sampledStart);
                if ((sampledGoal - sampledStart).sqrMagnitude > 0.01f)
                {
                    result.Add(sampledGoal);
                }
                return true;
            }

            Array.Fill(costs, float.PositiveInfinity);
            Array.Fill(previous, -1);
            Array.Clear(closed, 0, closed.Length);
            open.Clear();
            reversePath.Clear();

            costs[startNode] = 0f;
            open.Add(startNode);
            bool found = false;
            int expanded = 0;
            int expansionLimit = Mathf.Max(4096, nodes.Count * 2);

            while (open.Count > 0 && expanded < expansionLimit)
            {
                int bestSlot = FindBestOpenSlot(goalNode);
                int current = open[bestSlot];
                open.RemoveAt(bestSlot);
                if (closed[current])
                {
                    continue;
                }

                if (current == goalNode)
                {
                    found = true;
                    break;
                }

                closed[current] = true;
                expanded++;
                List<Connection> connections = nodes[current].Connections;
                for (int i = 0; i < connections.Count; i++)
                {
                    Connection connection = connections[i];
                    if (closed[connection.Target])
                    {
                        continue;
                    }

                    float proposed = costs[current] + connection.Cost;
                    if (proposed >= costs[connection.Target])
                    {
                        continue;
                    }

                    costs[connection.Target] = proposed;
                    previous[connection.Target] = current;
                    open.Add(connection.Target);
                }
            }

            if (!found)
            {
                return false;
            }

            int cursor = goalNode;
            reversePath.Add(cursor);
            while (cursor != startNode)
            {
                cursor = previous[cursor];
                if (cursor < 0)
                {
                    return false;
                }
                reversePath.Add(cursor);
            }
            reversePath.Reverse();

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
            return true;
        }

        private int FindBestOpenSlot(int goal)
        {
            int bestSlot = 0;
            float bestScore = costs[open[0]] + Vector3.Distance(nodes[open[0]].Position, nodes[goal].Position);
            for (int i = 1; i < open.Count; i++)
            {
                int node = open[i];
                float score = costs[node] + Vector3.Distance(nodes[node].Position, nodes[goal].Position);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestSlot = i;
                }
            }
            return bestSlot;
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
