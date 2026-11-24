using System;
using System.Collections.Generic;

namespace BlackoutProtocol.AI.Hunter.Logic
{
    /// <summary>An acoustic-only connection, normally spanning the two sides of a doorway.</summary>
    public readonly struct HunterAcousticPortal
    {
        public HunterAcousticPortal(int firstNode, int secondNode, double distance, float attenuation, bool closed)
        {
            FirstNode = firstNode;
            SecondNode = secondNode;
            Distance = distance;
            Attenuation = attenuation;
            Closed = closed;
        }

        public int FirstNode { get; }
        public int SecondNode { get; }
        public double Distance { get; }
        public float Attenuation { get; }
        public bool Closed { get; }
    }

    /// <summary>Result of bounded acoustic travel from a sound source to a listener.</summary>
    public readonly struct HunterSoundPropagationResult
    {
        public HunterSoundPropagationResult(bool heard, float originalLoudness, float perceivedLoudness,
            double travelDistance, int closedDoors, int nodesExpanded, HunterNavPoint incomingPoint)
        {
            Heard = heard;
            OriginalLoudness = originalLoudness;
            PerceivedLoudness = perceivedLoudness;
            TravelDistance = travelDistance;
            ClosedDoors = closedDoors;
            NodesExpanded = nodesExpanded;
            IncomingPoint = incomingPoint;
        }

        public bool Heard { get; }
        public float OriginalLoudness { get; }
        public float PerceivedLoudness { get; }
        public double TravelDistance { get; }
        public int ClosedDoors { get; }
        public int NodesExpanded { get; }
        public HunterNavPoint IncomingPoint { get; }
    }

    /// <summary>
    /// Bounded Dijkstra search for game-audio perception. Travel consumes loudness as
    /// <c>L = L0 - distanceLoss * pathDistance - sum(doorAttenuation)</c>.
    /// It is deliberately independent of Unity and does not model reflected audio.
    /// </summary>
    public static class HunterSoundPropagation
    {
        public static HunterSoundPropagationResult Propagate(HunterNavGraph graph, int sourceNode,
            int listenerNode, float originalLoudness, double distanceLoss = 1d,
            IReadOnlyList<HunterAcousticPortal> portals = null, int maximumExpandedNodes = 4096)
        {
            if (graph == null || !graph.IsValidNode(sourceNode) || !graph.IsValidNode(listenerNode) ||
                !Finite(originalLoudness) || originalLoudness <= 0f || !Finite(distanceLoss) ||
                distanceLoss < 0d || maximumExpandedNodes < 1)
                return default;

            int count = graph.NodeCount;
            var cost = new double[count];
            var distance = new double[count];
            var previous = new int[count];
            var closedDoors = new int[count];
            for (int i = 0; i < count; i++)
            {
                cost[i] = distance[i] = double.PositiveInfinity;
                previous[i] = -1;
                closedDoors[i] = int.MaxValue;
            }

            var queue = new HunterPriorityQueue();
            cost[sourceNode] = 0d;
            distance[sourceNode] = 0d;
            closedDoors[sourceNode] = 0;
            queue.Enqueue(sourceNode, 0d, 0d);
            int expanded = 0;

            while (queue.TryDequeue(out HunterPriorityQueue.Entry entry) && expanded < maximumExpandedNodes)
            {
                if (entry.G > cost[entry.Node] + 1e-9) continue;
                if (entry.Node == listenerNode) break;
                expanded++;

                IReadOnlyList<HunterNavEdge> edges = graph.GetEdges(entry.Node);
                for (int i = 0; i < edges.Count; i++)
                    Relax(entry.Node, edges[i].To, edges[i].Cost, 0f, false, originalLoudness,
                        distanceLoss, cost, distance, previous, closedDoors, queue);

                if (portals == null) continue;
                for (int i = 0; i < portals.Count; i++)
                {
                    HunterAcousticPortal portal = portals[i];
                    int other = portal.FirstNode == entry.Node ? portal.SecondNode :
                        portal.SecondNode == entry.Node ? portal.FirstNode : -1;
                    if (other >= 0)
                        Relax(entry.Node, other, portal.Distance, portal.Attenuation, portal.Closed,
                            originalLoudness, distanceLoss, cost, distance, previous, closedDoors, queue);
                }
            }

            if (double.IsInfinity(cost[listenerNode])) return default;
            float remaining = (float)Math.Max(0d, originalLoudness - cost[listenerNode]);
            if (remaining <= 0f) return new HunterSoundPropagationResult(false, originalLoudness, 0f,
                distance[listenerNode], closedDoors[listenerNode], expanded, graph.GetPoint(listenerNode));

            int incoming = listenerNode;
            while (previous[incoming] >= 0 && previous[incoming] != sourceNode)
                incoming = previous[incoming];
            // Walking predecessors from listener reaches the node immediately on the source side.
            if (previous[listenerNode] >= 0) incoming = previous[listenerNode];
            return new HunterSoundPropagationResult(true, originalLoudness, remaining,
                distance[listenerNode], closedDoors[listenerNode], expanded, graph.GetPoint(incoming));
        }

        private static void Relax(int from, int to, double edgeDistance, float attenuation, bool closed,
            float originalLoudness, double distanceLoss, double[] cost, double[] distance, int[] previous,
            int[] closedDoors, HunterPriorityQueue queue)
        {
            if (to < 0 || to >= cost.Length || !Finite(edgeDistance) || edgeDistance < 0d ||
                !Finite(attenuation) || attenuation < 0f) return;
            double nextCost = cost[from] + edgeDistance * distanceLoss + attenuation;
            if (nextCost >= originalLoudness || nextCost + 1e-9 >= cost[to]) return;
            cost[to] = nextCost;
            distance[to] = distance[from] + edgeDistance;
            previous[to] = from;
            closedDoors[to] = closedDoors[from] + (closed ? 1 : 0);
            queue.Enqueue(to, nextCost, 0d);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
