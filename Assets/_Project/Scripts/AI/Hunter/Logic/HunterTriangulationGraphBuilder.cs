using System;
using System.Collections.Generic;

namespace BlackoutProtocol.AI.Hunter.Logic
{
    /// <summary>Connects triangle centroids only across shared, traversable triangle edges.</summary>
    public static class HunterTriangulationGraphBuilder
    {
        private struct VertexKey : IEquatable<VertexKey>, IComparable<VertexKey>
        {
            private readonly double x, y, z;
            public VertexKey(HunterNavPoint p)
            {
                x = Math.Round(p.X * 1000.0);
                y = Math.Round(p.Y * 1000.0);
                z = Math.Round(p.Z * 1000.0);
            }
            public bool Equals(VertexKey other) => x == other.x && y == other.y && z == other.z;
            public override bool Equals(object other) => other is VertexKey key && Equals(key);
            public override int GetHashCode() { unchecked { return ((x.GetHashCode() * 397) ^ y.GetHashCode()) * 397 ^ z.GetHashCode(); } }
            public int CompareTo(VertexKey other)
            {
                int c = x.CompareTo(other.x);
                if (c != 0) return c;
                c = y.CompareTo(other.y);
                return c != 0 ? c : z.CompareTo(other.z);
            }
        }

        private struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly VertexKey a, b;
            public EdgeKey(HunterNavPoint first, HunterNavPoint second)
            {
                var x = new VertexKey(first);
                var y = new VertexKey(second);
                a = x.CompareTo(y) <= 0 ? x : y;
                b = x.CompareTo(y) <= 0 ? y : x;
            }
            public bool Equals(EdgeKey other) => a.Equals(other.a) && b.Equals(other.b);
            public override bool Equals(object other) => other is EdgeKey key && Equals(key);
            public override int GetHashCode() { unchecked { return a.GetHashCode() * 397 ^ b.GetHashCode(); } }
        }

        private struct Owner
        {
            public int Node;
            public bool Paired;
        }

        public static HunterNavGraph Build(HunterNavPoint[] vertices, int[] indices,
            Func<HunterNavPoint, HunterNavPoint, bool> canTraverse = null)
        {
            var graph = new HunterNavGraph();
            if (vertices == null || indices == null) return graph;
            var owners = new Dictionary<EdgeKey, Owner>();
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                if (ia < 0 || ib < 0 || ic < 0 || ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length ||
                    ia == ib || ib == ic || ia == ic) continue;
                HunterNavPoint a = vertices[ia], b = vertices[ib], c = vertices[ic];
                if (!a.IsFinite || !b.IsFinite || !c.IsFinite || Degenerate(a, b, c)) continue;
                var center = new HunterNavPoint((float)(((double)a.X + b.X + c.X) / 3),
                    (float)(((double)a.Y + b.Y + c.Y) / 3), (float)(((double)a.Z + b.Z + c.Z) / 3));
                int node = graph.AddNode(center);
                if (node < 0) continue;
                Connect(new EdgeKey(a, b), node, graph, owners, canTraverse);
                Connect(new EdgeKey(b, c), node, graph, owners, canTraverse);
                Connect(new EdgeKey(c, a), node, graph, owners, canTraverse);
            }
            return graph;
        }

        private static void Connect(EdgeKey key, int node, HunterNavGraph graph,
            Dictionary<EdgeKey, Owner> owners, Func<HunterNavPoint, HunterNavPoint, bool> canTraverse)
        {
            if (!owners.TryGetValue(key, out Owner owner))
            {
                owners.Add(key, new Owner { Node = node });
                return;
            }
            if (owner.Paired) return; // malformed non-manifold edge
            owner.Paired = true;
            owners[key] = owner;
            HunterNavPoint from = graph.GetPoint(owner.Node), to = graph.GetPoint(node);
            if (canTraverse != null && !canTraverse(from, to)) return;
            graph.TryConnectBoth(owner.Node, node, HunterNavPoint.Distance(from, to));
        }

        private static bool Degenerate(HunterNavPoint a, HunterNavPoint b, HunterNavPoint c)
        {
            double ux = (double)b.X - a.X, uy = (double)b.Y - a.Y, uz = (double)b.Z - a.Z;
            double vx = (double)c.X - a.X, vy = (double)c.Y - a.Y, vz = (double)c.Z - a.Z;
            double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
            return cx * cx + cy * cy + cz * cz < 1e-12;
        }
    }
}
