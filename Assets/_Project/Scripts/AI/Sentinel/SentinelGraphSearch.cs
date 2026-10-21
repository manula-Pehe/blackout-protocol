using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace BlackoutProtocol.AI.Sentinel
{
    public readonly struct SentinelGraphEdge
    {
        public SentinelGraphEdge(int target, float cost) { Target = target; Cost = cost; }
        public int Target { get; }
        public float Cost { get; }
    }

    /// <summary>Reusable A*/uniform-cost search over a fixed weighted graph.</summary>
    public sealed class SentinelGraphSearch
    {
        private readonly Vector3[] positions;
        private readonly SentinelGraphEdge[][] edges;
        private readonly float[] costs;
        private readonly int[] previous;
        private readonly bool[] closed;
        private readonly List<int> open = new List<int>();

        public SentinelGraphSearch(Vector3[] positions, SentinelGraphEdge[][] edges)
        {
            this.positions = positions ?? Array.Empty<Vector3>();
            this.edges = edges ?? Array.Empty<SentinelGraphEdge[]>();
            costs = new float[this.positions.Length];
            previous = new int[this.positions.Length];
            closed = new bool[this.positions.Length];
        }

        public SentinelPathSearchStats Search(int start, int goal, bool useHeuristic, List<int> path)
        {
            long started = Stopwatch.GetTimestamp();
            path.Clear();
            if (start < 0 || goal < 0 || start >= positions.Length || goal >= positions.Length ||
                edges.Length != positions.Length)
                return Finish(false, 0f, 0, started);

            Array.Fill(costs, float.PositiveInfinity);
            Array.Fill(previous, -1);
            Array.Clear(closed, 0, closed.Length);
            open.Clear();
            costs[start] = 0f;
            open.Add(start);
            int expanded = 0;
            int expansionLimit = Mathf.Max(4096, positions.Length * 2);
            bool found = false;

            while (open.Count > 0 && expanded < expansionLimit)
            {
                int bestSlot = FindBestOpenSlot(goal, useHeuristic);
                int current = open[bestSlot];
                open.RemoveAt(bestSlot);
                if (closed[current]) continue;
                closed[current] = true;
                expanded++;
                if (current == goal) { found = true; break; }

                SentinelGraphEdge[] connections = edges[current];
                for (int i = 0; i < connections.Length; i++)
                {
                    SentinelGraphEdge connection = connections[i];
                    if (connection.Target < 0 || connection.Target >= positions.Length || closed[connection.Target]) continue;
                    float proposed = costs[current] + connection.Cost;
                    if (proposed >= costs[connection.Target]) continue;
                    costs[connection.Target] = proposed;
                    previous[connection.Target] = current;
                    open.Add(connection.Target);
                }
            }

            if (found)
            {
                for (int cursor = goal; cursor >= 0; cursor = previous[cursor])
                {
                    path.Add(cursor);
                    if (cursor == start) break;
                }
                path.Reverse();
                if (path.Count == 0 || path[0] != start) found = false;
            }
            return Finish(found, found ? costs[goal] : 0f, expanded, started);
        }

        private int FindBestOpenSlot(int goal, bool useHeuristic)
        {
            int bestSlot = 0;
            float bestScore = Score(open[0], goal, useHeuristic);
            for (int i = 1; i < open.Count; i++)
            {
                float score = Score(open[i], goal, useHeuristic);
                if (score < bestScore) { bestScore = score; bestSlot = i; }
            }
            return bestSlot;
        }

        private float Score(int node, int goal, bool useHeuristic) =>
            costs[node] + (useHeuristic ? Vector3.Distance(positions[node], positions[goal]) : 0f);

        private static SentinelPathSearchStats Finish(bool succeeded, float cost, int expanded, long started) =>
            new SentinelPathSearchStats(succeeded, cost, expanded,
                (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
    }
}
