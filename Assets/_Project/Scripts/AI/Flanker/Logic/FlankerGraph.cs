using System;
using System.Collections.Generic;

namespace BlackoutProtocol.AI.Flanker.Logic
{
    /// <summary>Flanker-owned triangle-centroid/portal graph with geometric edge lengths.</summary>
    public sealed class FlankerGraph
    {
        private readonly FlankerPoint[] points;
        private readonly List<int>[] neighbours;

        public FlankerGraph(IReadOnlyList<FlankerPoint> positions)
        {
            points = new FlankerPoint[positions.Count];
            neighbours = new List<int>[positions.Count];
            for (int index = 0; index < positions.Count; index++)
            {
                points[index] = positions[index];
                neighbours[index] = new List<int>(3);
            }
        }

        public int Count => points.Length;
        public FlankerPoint Point(int index) => points[index];
        public IReadOnlyList<int> Neighbours(int index) => neighbours[index];

        public void Connect(int first, int second)
        {
            if (first < 0 || second < 0 || first >= Count || second >= Count) throw new ArgumentOutOfRangeException();
            if (first == second || neighbours[first].Contains(second)) return;
            neighbours[first].Add(second);
            neighbours[second].Add(first);
        }
    }

    /// <summary>Reusable bounded A*. Exposure is frozen by the caller for the duration of a search.</summary>
    public sealed class FlankerAStar
    {
        private readonly FlankerGraph graph;
        private readonly double[] costs;
        private readonly int[] parents;
        private readonly bool[] closed;

        public FlankerAStar(FlankerGraph graph)
        {
            this.graph = graph;
            costs = new double[graph.Count];
            parents = new int[graph.Count];
            closed = new bool[graph.Count];
        }

        public int Expanded { get; private set; }

        /// <summary>Edge cost is distance times one plus nonnegative mean exposure penalty; h is Euclidean distance.</summary>
        public bool Find(int start, int goal, float[] exposure, float weight, List<int> result,
            out double cost, int maximumExpanded = 4096, bool heuristic = true)
        {
            result.Clear();
            cost = double.PositiveInfinity;
            Expanded = 0;
            if (start < 0 || goal < 0 || start >= graph.Count || goal >= graph.Count || maximumExpanded < 1) return false;
            if (exposure == null || exposure.Length != graph.Count) throw new ArgumentException("Exposure must match the graph.");
            if (!FlankerPoint.Finite(weight) || weight < 0f) throw new ArgumentOutOfRangeException(nameof(weight));
            for (int index = 0; index < graph.Count; index++)
            {
                if (!FlankerPoint.Finite(exposure[index]) || exposure[index] < 0f || exposure[index] > 1f)
                    throw new ArgumentException("Exposure must be finite and normalized.");
                costs[index] = double.PositiveInfinity;
                parents[index] = -1;
                closed[index] = false;
            }
            costs[start] = 0d;
            while (Expanded <= maximumExpanded)
            {
                int current = -1;
                double best = double.PositiveInfinity;
                for (int index = 0; index < graph.Count; index++)
                {
                    if (closed[index] || double.IsPositiveInfinity(costs[index])) continue;
                    double estimate = costs[index] + (heuristic ? FlankerPoint.Distance(graph.Point(index), graph.Point(goal)) : 0d);
                    if (estimate < best)
                    {
                        best = estimate;
                        current = index;
                    }
                }
                if (current < 0) return false;
                if (current == goal)
                {
                    cost = costs[current];
                    for (int cursor = goal; cursor >= 0; cursor = parents[cursor]) result.Add(cursor);
                    result.Reverse();
                    return true;
                }
                if (Expanded >= maximumExpanded) return false;
                closed[current] = true;
                Expanded++;
                IReadOnlyList<int> adjacent = graph.Neighbours(current);
                for (int index = 0; index < adjacent.Count; index++)
                {
                    int neighbour = adjacent[index];
                    if (closed[neighbour]) continue;
                    double distance = FlankerPoint.Distance(graph.Point(current), graph.Point(neighbour));
                    double proposed = costs[current] + distance * (1d + weight * ((double)exposure[current] + exposure[neighbour]) * 0.5d);
                    if (proposed >= costs[neighbour]) continue;
                    costs[neighbour] = proposed;
                    parents[neighbour] = current;
                }
            }
            return false;
        }
    }
}
