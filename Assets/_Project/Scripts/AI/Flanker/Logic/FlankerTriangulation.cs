using System;
using System.Collections.Generic;

namespace BlackoutProtocol.AI.Flanker.Logic
{
    /// <summary>Welds full shared edges to 1 mm; vertex-only and non-manifold contacts do not connect.</summary>
    public static class FlankerTriangulation
    {
        private readonly struct Vertex : IEquatable<Vertex>, IComparable<Vertex>
        {
            private readonly long horizontal;
            private readonly long height;
            private readonly long depth;

            public Vertex(FlankerPoint point)
            {
                horizontal = checked((long)Math.Round(point.X * 1000d));
                height = checked((long)Math.Round(point.Y * 1000d));
                depth = checked((long)Math.Round(point.Z * 1000d));
            }

            public int CompareTo(Vertex other)
            {
                int comparison = horizontal.CompareTo(other.horizontal);
                if (comparison == 0) comparison = height.CompareTo(other.height);
                return comparison != 0 ? comparison : depth.CompareTo(other.depth);
            }

            public bool Equals(Vertex other) => horizontal == other.horizontal && height == other.height && depth == other.depth;
            public override bool Equals(object other) => other is Vertex vertex && Equals(vertex);
            public override int GetHashCode() => unchecked((horizontal.GetHashCode() * 397 ^ height.GetHashCode()) * 397 ^ depth.GetHashCode());
        }

        private readonly struct Edge : IEquatable<Edge>
        {
            private readonly Vertex first;
            private readonly Vertex second;

            public Edge(FlankerPoint start, FlankerPoint end)
            {
                Vertex startKey = new Vertex(start);
                Vertex endKey = new Vertex(end);
                bool ordered = startKey.CompareTo(endKey) <= 0;
                first = ordered ? startKey : endKey;
                second = ordered ? endKey : startKey;
                Midpoint = new FlankerPoint((start.X + end.X) * 0.5f, (start.Y + end.Y) * 0.5f, (start.Z + end.Z) * 0.5f);
            }

            public FlankerPoint Midpoint { get; }
            public bool Equals(Edge other) => first.Equals(other.first) && second.Equals(other.second);
            public override bool Equals(object other) => other is Edge edge && Equals(edge);
            public override int GetHashCode() => unchecked(first.GetHashCode() * 397 ^ second.GetHashCode());
        }

        /// <summary>Builds adjacency; the adapter can additionally reject blocked centroid connections.</summary>
        public static FlankerGraph Build(FlankerPoint[] vertices, int[] indices, Func<FlankerPoint, FlankerPoint, bool> walkable = null)
        {
            if (vertices == null || indices == null || indices.Length % 3 != 0) throw new ArgumentException("Expected indexed triangles.");
            var points = new List<FlankerPoint>(indices.Length / 3);
            var owners = new Dictionary<Edge, List<int>>();
            for (int offset = 0; offset < indices.Length; offset += 3)
            {
                int first = indices[offset];
                int second = indices[offset + 1];
                int third = indices[offset + 2];
                if (first < 0 || second < 0 || third < 0 || first >= vertices.Length || second >= vertices.Length || third >= vertices.Length)
                    throw new ArgumentException("Triangle index is outside the vertex array.");
                FlankerPoint start = vertices[first];
                FlankerPoint middle = vertices[second];
                FlankerPoint end = vertices[third];
                double firstX = middle.X - (double)start.X;
                double firstY = middle.Y - (double)start.Y;
                double firstZ = middle.Z - (double)start.Z;
                double secondX = end.X - (double)start.X;
                double secondY = end.Y - (double)start.Y;
                double secondZ = end.Z - (double)start.Z;
                double crossX = firstY * secondZ - firstZ * secondY;
                double crossY = firstZ * secondX - firstX * secondZ;
                double crossZ = firstX * secondY - firstY * secondX;
                if (crossX * crossX + crossY * crossY + crossZ * crossZ < 0.00000001d) continue;
                int node = points.Count;
                points.Add(new FlankerPoint((start.X + middle.X + end.X) / 3f, (start.Y + middle.Y + end.Y) / 3f, (start.Z + middle.Z + end.Z) / 3f));
                AddOwner(owners, new Edge(start, middle), node);
                AddOwner(owners, new Edge(middle, end), node);
                AddOwner(owners, new Edge(end, start), node);
            }
            var connections = new List<KeyValuePair<int, int>>();
            foreach (KeyValuePair<Edge, List<int>> entry in owners)
            {
                List<int> adjacent = entry.Value;
                if (adjacent.Count != 2) continue;
                int first = adjacent[0];
                int second = adjacent[1];
                if (walkable == null || walkable(points[first], points[second]))
                    connections.Add(new KeyValuePair<int, int>(first, second));
                else
                {
                    FlankerPoint portal = entry.Key.Midpoint;
                    if (!walkable(points[first], portal) || !walkable(portal, points[second])) continue;
                    int portalIndex = points.Count;
                    points.Add(portal);
                    connections.Add(new KeyValuePair<int, int>(first, portalIndex));
                    connections.Add(new KeyValuePair<int, int>(portalIndex, second));
                }
            }
            var graph = new FlankerGraph(points);
            foreach (KeyValuePair<int, int> connection in connections) graph.Connect(connection.Key, connection.Value);
            return graph;
        }

        private static void AddOwner(Dictionary<Edge, List<int>> owners, Edge edge, int node)
        {
            if (!owners.TryGetValue(edge, out List<int> adjacent))
            {
                adjacent = new List<int>(2);
                owners.Add(edge, adjacent);
            }
            adjacent.Add(node);
        }
    }
}
