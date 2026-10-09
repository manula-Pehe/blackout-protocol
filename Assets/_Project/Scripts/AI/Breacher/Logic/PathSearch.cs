using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace BlackoutProtocol.AI.Breacher.Logic
{
    /// <summary>
    /// The path and measurements produced by one graph search.
    /// </summary>
    public sealed class PathSearchResult
    {
        public PathSearchResult(
            bool succeeded,
            IReadOnlyList<NavNode> path,
            float totalCost,
            int expandedNodeCount,
            double elapsedMilliseconds)
        {
            Succeeded = succeeded;
            Path = path;
            TotalCost = totalCost;
            ExpandedNodeCount = expandedNodeCount;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public bool Succeeded { get; }
        public IReadOnlyList<NavNode> Path { get; }
        public float TotalCost { get; }
        public int ExpandedNodeCount { get; }
        public double ElapsedMilliseconds { get; }
    }

    /// <summary>
    /// Searches a navigation graph using A* or Uniform Cost Search.
    /// </summary>
    public static class PathSearch
    {
        private sealed class OpenRecord
        {
            public OpenRecord(NavNode node, float estimatedTotalCost)
            {
                Node = node;
                EstimatedTotalCost = estimatedTotalCost;
            }

            public NavNode Node { get; }
            public float EstimatedTotalCost { get; }
        }

        public static PathSearchResult FindWithAStar(
            NavNode start,
            NavNode goal,
            int maximumExpandedNodes = 4096)
        {
            return FindPath(
                start,
                goal,
                (node, destination) =>
                    Vector3.Distance(node.Position, destination.Position),
                maximumExpandedNodes);
        }

        public static PathSearchResult FindWithUcs(
            NavNode start,
            NavNode goal,
            int maximumExpandedNodes = 4096)
        {
            return FindPath(
                start,
                goal,
                (node, destination) => 0f,
                maximumExpandedNodes);
        }

        private static PathSearchResult FindPath(
            NavNode start,
            NavNode goal,
            Func<NavNode, NavNode, float> heuristic,
            int maximumExpandedNodes)
        {
            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            if (goal == null)
            {
                throw new ArgumentNullException(nameof(goal));
            }

            if (maximumExpandedNodes <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumExpandedNodes));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            var open = new List<OpenRecord>();
            var closed = new HashSet<NavNode>();
            var costs = new Dictionary<NavNode, float>();
            var previous = new Dictionary<NavNode, NavNode>();

            costs[start] = 0f;
            open.Add(new OpenRecord(start, heuristic(start, goal)));

            int expandedNodeCount = 0;

            while (open.Count > 0 &&
                   expandedNodeCount < maximumExpandedNodes)
            {
                int bestIndex = FindLowestCostIndex(open);
                NavNode current = open[bestIndex].Node;
                open.RemoveAt(bestIndex);

                if (closed.Contains(current))
                {
                    continue;
                }

                if (current == goal)
                {
                    stopwatch.Stop();
                    return CreateSuccessResult(
                        start,
                        goal,
                        previous,
                        costs[goal],
                        expandedNodeCount,
                        stopwatch.Elapsed.TotalMilliseconds);
                }

                closed.Add(current);
                expandedNodeCount++;

                foreach (NavEdge edge in current.Edges)
                {
                    if (closed.Contains(edge.Target))
                    {
                        continue;
                    }

                    float proposedCost = costs[current] + edge.Cost;

                    if (costs.TryGetValue(edge.Target, out float knownCost) &&
                        proposedCost >= knownCost)
                    {
                        continue;
                    }

                    costs[edge.Target] = proposedCost;
                    previous[edge.Target] = current;

                    float estimatedTotalCost =
                        proposedCost + heuristic(edge.Target, goal);

                    open.Add(new OpenRecord(
                        edge.Target,
                        estimatedTotalCost));
                }
            }

            stopwatch.Stop();
            return new PathSearchResult(
                false,
                Array.Empty<NavNode>(),
                float.PositiveInfinity,
                expandedNodeCount,
                stopwatch.Elapsed.TotalMilliseconds);
        }

        private static int FindLowestCostIndex(List<OpenRecord> open)
        {
            int bestIndex = 0;
            float bestCost = open[0].EstimatedTotalCost;

            for (int index = 1; index < open.Count; index++)
            {
                if (open[index].EstimatedTotalCost < bestCost)
                {
                    bestIndex = index;
                    bestCost = open[index].EstimatedTotalCost;
                }
            }

            return bestIndex;
        }

        private static PathSearchResult CreateSuccessResult(
            NavNode start,
            NavNode goal,
            IReadOnlyDictionary<NavNode, NavNode> previous,
            float totalCost,
            int expandedNodeCount,
            double elapsedMilliseconds)
        {
            var path = new List<NavNode>();
            NavNode current = goal;
            path.Add(current);

            while (current != start)
            {
                current = previous[current];
                path.Add(current);
            }

            path.Reverse();

            return new PathSearchResult(
                true,
                path,
                totalCost,
                expandedNodeCount,
                elapsedMilliseconds);
        }
    }
}
