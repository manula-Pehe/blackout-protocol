using System;
using System.Collections.Generic;

namespace BlackoutProtocol.AI.Hunter.Logic
{
    public struct HunterNavPoint
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public HunterNavPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool IsFinite => IsFiniteNumber(X) && IsFiniteNumber(Y) && IsFiniteNumber(Z);

        private static bool IsFiniteNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static double Distance(HunterNavPoint a, HunterNavPoint b)
        {
            double dx = (double)a.X - b.X;
            double dy = (double)a.Y - b.Y;
            double dz = (double)a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    public struct HunterNavEdge
    {
        public readonly int To;
        public readonly double Cost;

        public HunterNavEdge(int to, double cost)
        {
            To = to;
            Cost = cost;
        }
    }

    /// <summary>Weighted graph. Every accepted edge costs at least its 3D straight-line distance.</summary>
    public sealed class HunterNavGraph
    {
        private readonly List<HunterNavPoint> points = new List<HunterNavPoint>();
        private readonly List<List<HunterNavEdge>> adjacency = new List<List<HunterNavEdge>>();

        public int NodeCount => points.Count;

        public int AddNode(HunterNavPoint point)
        {
            if (!point.IsFinite) return -1;
            int id = points.Count;
            points.Add(point);
            adjacency.Add(new List<HunterNavEdge>());
            return id;
        }

        public bool IsValidNode(int id) => id >= 0 && id < points.Count;

        public HunterNavPoint GetPoint(int id) => points[id];

        public IReadOnlyList<HunterNavEdge> GetEdges(int id) => adjacency[id];

        public bool TryConnect(int from, int to, double cost)
        {
            if (!IsValidNode(from) || !IsValidNode(to) || from == to ||
                double.IsNaN(cost) || double.IsInfinity(cost) || cost < 0 ||
                cost + 1e-6 < HunterNavPoint.Distance(points[from], points[to])) return false;

            var edges = adjacency[from];
            for (int i = 0; i < edges.Count; i++)
                if (edges[i].To == to) return false;
            edges.Add(new HunterNavEdge(to, cost));
            return true;
        }

        public bool TryConnectBoth(int a, int b, double cost)
        {
            if (!TryConnect(a, b, cost)) return false;
            TryConnect(b, a, cost);
            return true;
        }
    }
}
