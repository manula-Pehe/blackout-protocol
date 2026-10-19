using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BlackoutProtocol.AI.Hunter.Logic
{
    public sealed class HunterPathResult
    {
        public bool Succeeded { get; internal set; }
        public double TotalCost { get; internal set; }
        public int NodesExpanded { get; internal set; }
        public double ElapsedMilliseconds { get; internal set; }
        public IReadOnlyList<int> NodePath { get; internal set; } = new int[0];
    }

    /// <summary>A* and UCS differ only in their heuristic; both use the same graph and heap.</summary>
    public static class HunterPathSearch
    {
        public static HunterPathResult AStar(HunterNavGraph graph, int start, int goal, int maximumExpandedNodes = 4096)
            => Search(graph, start, goal, maximumExpandedNodes, true);

        public static HunterPathResult UniformCost(HunterNavGraph graph, int start, int goal, int maximumExpandedNodes = 4096)
            => Search(graph, start, goal, maximumExpandedNodes, false);

        private static HunterPathResult Search(HunterNavGraph graph, int start, int goal, int limit, bool useHeuristic)
        {
            var result = new HunterPathResult();
            var watch = Stopwatch.StartNew();
            try
            {
                if (graph == null || !graph.IsValidNode(start) || !graph.IsValidNode(goal) || limit <= 0)
                    return result;
                if (start == goal)
                {
                    result.Succeeded = true;
                    result.NodePath = new[] { start };
                    return result;
                }

                int count = graph.NodeCount;
                var best = new double[count];
                var parent = new int[count];
                for (int i = 0; i < count; i++) { best[i] = double.PositiveInfinity; parent[i] = -1; }
                var open = new HunterPriorityQueue();
                best[start] = 0;
                open.Enqueue(start, 0, Heuristic(graph, start, goal, useHeuristic));

                while (open.TryDequeue(out HunterPriorityQueue.Entry current))
                {
                    if (current.G > best[current.Node]) continue; // obsolete heap entry
                    if (result.NodesExpanded >= limit) break;
                    result.NodesExpanded++;
                    if (current.Node == goal)
                    {
                        var reverse = new List<int>();
                        for (int node = goal; node >= 0; node = parent[node]) reverse.Add(node);
                        reverse.Reverse();
                        result.NodePath = reverse;
                        result.TotalCost = best[goal];
                        result.Succeeded = true;
                        return result;
                    }

                    var edges = graph.GetEdges(current.Node);
                    for (int i = 0; i < edges.Count; i++)
                    {
                        HunterNavEdge edge = edges[i];
                        if (!graph.IsValidNode(edge.To) || double.IsNaN(edge.Cost) ||
                            double.IsInfinity(edge.Cost) || edge.Cost < 0) continue;
                        double candidate = current.G + edge.Cost;
                        if (double.IsInfinity(candidate) || candidate >= best[edge.To]) continue;
                        double h = Heuristic(graph, edge.To, goal, useHeuristic);
                        if (!open.Enqueue(edge.To, candidate, h)) continue;
                        best[edge.To] = candidate;
                        parent[edge.To] = current.Node;
                    }
                }
                return result;
            }
            finally
            {
                watch.Stop();
                result.ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds;
            }
        }

        private static double Heuristic(HunterNavGraph graph, int node, int goal, bool enabled)
            => enabled ? HunterNavPoint.Distance(graph.GetPoint(node), graph.GetPoint(goal)) : 0;
    }
}
