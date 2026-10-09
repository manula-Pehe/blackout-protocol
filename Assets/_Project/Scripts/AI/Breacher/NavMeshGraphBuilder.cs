using System;
using System.Collections.Generic;
using BlackoutProtocol.AI.Breacher.Logic;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.AI.Breacher
{
    /// <summary>
    /// Converts Unity's baked NavMesh triangles into a graph that can be
    /// searched by the Breacher's own A* implementation.
    /// </summary>
    public static class NavMeshGraphBuilder
    {
        private readonly struct VertexKey :
            IEquatable<VertexKey>,
            IComparable<VertexKey>
        {
            private const float Precision = 1000f;

            public VertexKey(Vector3 position)
            {
                X = Mathf.RoundToInt(position.x * Precision);
                Y = Mathf.RoundToInt(position.y * Precision);
                Z = Mathf.RoundToInt(position.z * Precision);
            }

            public int X { get; }
            public int Y { get; }
            public int Z { get; }

            public bool Equals(VertexKey other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is VertexKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = X;
                    hash = (hash * 397) ^ Y;
                    hash = (hash * 397) ^ Z;
                    return hash;
                }
            }

            public int CompareTo(VertexKey other)
            {
                int xComparison = X.CompareTo(other.X);

                if (xComparison != 0)
                {
                    return xComparison;
                }

                int yComparison = Y.CompareTo(other.Y);
                return yComparison != 0
                    ? yComparison
                    : Z.CompareTo(other.Z);
            }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public EdgeKey(Vector3 firstVertex, Vector3 secondVertex)
            {
                var firstKey = new VertexKey(firstVertex);
                var secondKey = new VertexKey(secondVertex);

                if (firstKey.CompareTo(secondKey) <= 0)
                {
                    First = firstKey;
                    Second = secondKey;
                }
                else
                {
                    First = secondKey;
                    Second = firstKey;
                }
            }

            public VertexKey First { get; }
            public VertexKey Second { get; }

            public bool Equals(EdgeKey other)
            {
                return First.Equals(other.First) &&
                       Second.Equals(other.Second);
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (First.GetHashCode() * 397) ^
                           Second.GetHashCode();
                }
            }
        }

        public static NavGraph Build()
        {
            NavMeshTriangulation triangulation =
                NavMesh.CalculateTriangulation();

            var graph = new NavGraph();
            int triangleCount = triangulation.indices.Length / 3;

            if (triangleCount == 0)
            {
                return graph;
            }

            var triangleNodes = new NavNode[triangleCount];

            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                int indexOffset = triangle * 3;
                Vector3 first = triangulation.vertices[
                    triangulation.indices[indexOffset]];
                Vector3 second = triangulation.vertices[
                    triangulation.indices[indexOffset + 1]];
                Vector3 third = triangulation.vertices[
                    triangulation.indices[indexOffset + 2]];

                Vector3 centre = (first + second + third) / 3f;
                triangleNodes[triangle] = graph.AddNode(centre);
            }

            var edgeOwners = new Dictionary<EdgeKey, int>();

            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                int indexOffset = triangle * 3;
                int first = triangulation.indices[indexOffset];
                int second = triangulation.indices[indexOffset + 1];
                int third = triangulation.indices[indexOffset + 2];

                ConnectAcrossSharedEdge(
                    triangulation.vertices[first],
                    triangulation.vertices[second],
                    triangle,
                    triangleNodes,
                    edgeOwners,
                    graph);
                ConnectAcrossSharedEdge(
                    triangulation.vertices[second],
                    triangulation.vertices[third],
                    triangle,
                    triangleNodes,
                    edgeOwners,
                    graph);
                ConnectAcrossSharedEdge(
                    triangulation.vertices[third],
                    triangulation.vertices[first],
                    triangle,
                    triangleNodes,
                    edgeOwners,
                    graph);
            }

            return graph;
        }

        private static void ConnectAcrossSharedEdge(
            Vector3 firstVertex,
            Vector3 secondVertex,
            int triangle,
            IReadOnlyList<NavNode> triangleNodes,
            IDictionary<EdgeKey, int> edgeOwners,
            NavGraph graph)
        {
            var edge = new EdgeKey(firstVertex, secondVertex);

            if (!edgeOwners.TryGetValue(edge, out int neighbourTriangle))
            {
                edgeOwners.Add(edge, triangle);
                return;
            }

            NavNode current = triangleNodes[triangle];
            NavNode neighbour = triangleNodes[neighbourTriangle];
            float cost = Vector3.Distance(
                current.Position,
                neighbour.Position);

            graph.ConnectBidirectional(current, neighbour, cost);
        }
    }
}
