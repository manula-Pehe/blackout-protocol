using BlackoutProtocol.AI.Hunter.Logic;
using NUnit.Framework;

namespace BlackoutProtocol.AI.Hunter.Tests
{
    public sealed class HunterNavigationTests
    {
        private static HunterNavPoint P(float x, float z) => new HunterNavPoint(x, 0, z);

        [Test]
        public void StartEqualsGoal_ReturnsSingleNodeWithoutExpansion()
        {
            var graph = new HunterNavGraph();
            int node = graph.AddNode(P(0, 0));
            HunterPathResult result = HunterPathSearch.AStar(graph, node, node);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.NodePath, Is.EqualTo(new[] { node }));
            Assert.That(result.TotalCost, Is.Zero);
            Assert.That(result.NodesExpanded, Is.Zero);
        }

        [Test]
        public void DirectAndMultistepPaths_ReconstructInStartToGoalOrder()
        {
            var graph = new HunterNavGraph();
            int a = graph.AddNode(P(0, 0)), b = graph.AddNode(P(1, 0)), c = graph.AddNode(P(2, 0));
            graph.TryConnectBoth(a, b, 1);
            graph.TryConnectBoth(b, c, 1);
            Assert.That(HunterPathSearch.AStar(graph, a, b).NodePath, Is.EqualTo(new[] { a, b }));
            Assert.That(HunterPathSearch.AStar(graph, a, c).NodePath, Is.EqualTo(new[] { a, b, c }));
            Assert.That(HunterPathSearch.AStar(graph, c, a).NodePath, Is.EqualTo(new[] { c, b, a }));
        }

        [Test]
        public void CheapestRouteCanUseMoreHopsThanDirectRoute()
        {
            var graph = new HunterNavGraph();
            int a = graph.AddNode(P(0, 0)), b = graph.AddNode(P(1, 0)), c = graph.AddNode(P(2, 0));
            graph.TryConnectBoth(a, c, 10);
            graph.TryConnectBoth(a, b, 1);
            graph.TryConnectBoth(b, c, 1);
            HunterPathResult result = HunterPathSearch.AStar(graph, a, c);
            Assert.That(result.NodePath, Is.EqualTo(new[] { a, b, c }));
            Assert.That(result.TotalCost, Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void UnreachableAndInvalidNodesReturnFailure()
        {
            var graph = new HunterNavGraph();
            int a = graph.AddNode(P(0, 0)), b = graph.AddNode(P(1, 0));
            Assert.That(HunterPathSearch.AStar(graph, a, b).Succeeded, Is.False);
            Assert.That(HunterPathSearch.UniformCost(graph, -1, b).Succeeded, Is.False);
            Assert.That(HunterPathSearch.AStar(graph, a, 99).Succeeded, Is.False);
            Assert.That(HunterPathSearch.AStar(new HunterNavGraph(), 0, 0).Succeeded, Is.False);
            Assert.That(HunterPathSearch.AStar(null, 0, 0).Succeeded, Is.False);
            Assert.That(HunterPathSearch.AStar(graph, a, b, 0).Succeeded, Is.False);
        }

        [Test]
        public void AStarAndUcsAgreeOnCost_AndAStarExpandsNoMoreOnConsistentGraph()
        {
            var graph = new HunterNavGraph();
            int start = graph.AddNode(P(0, 0));
            int mid = graph.AddNode(P(1, 0));
            int goal = graph.AddNode(P(2, 0));
            int sideA = graph.AddNode(P(0, 1));
            int sideB = graph.AddNode(P(0, -1));
            graph.TryConnectBoth(start, mid, 1);
            graph.TryConnectBoth(mid, goal, 1);
            graph.TryConnectBoth(start, sideA, 1);
            graph.TryConnectBoth(start, sideB, 1);
            HunterPathResult aStar = HunterPathSearch.AStar(graph, start, goal);
            HunterPathResult ucs = HunterPathSearch.UniformCost(graph, start, goal);
            Assert.That(aStar.Succeeded && ucs.Succeeded, Is.True);
            Assert.That(aStar.TotalCost, Is.EqualTo(ucs.TotalCost).Within(1e-9));
            Assert.That(aStar.NodesExpanded, Is.LessThanOrEqualTo(ucs.NodesExpanded));
            Assert.That(aStar.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(ucs.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void EqualCostRoutesUseDeterministicNodeIdTieBreak()
        {
            var graph = new HunterNavGraph();
            int start = graph.AddNode(P(0, 0));
            int lowId = graph.AddNode(P(1, 1));
            int highId = graph.AddNode(P(1, -1));
            int goal = graph.AddNode(P(2, 0));
            graph.TryConnectBoth(start, highId, 2);
            graph.TryConnectBoth(start, lowId, 2);
            graph.TryConnectBoth(highId, goal, 2);
            graph.TryConnectBoth(lowId, goal, 2);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(HunterPathSearch.AStar(graph, start, goal).NodePath,
                    Is.EqualTo(new[] { start, lowId, goal }));
                Assert.That(HunterPathSearch.UniformCost(graph, start, goal).NodePath,
                    Is.EqualTo(new[] { start, lowId, goal }));
            }
        }

        [Test]
        public void GraphRejectsMalformedNodesAndEdges()
        {
            var graph = new HunterNavGraph();
            Assert.That(graph.AddNode(new HunterNavPoint(float.NaN, 0, 0)), Is.EqualTo(-1));
            int a = graph.AddNode(P(0, 0)), b = graph.AddNode(P(2, 0));
            Assert.That(graph.TryConnect(a, b, 1), Is.False); // below Euclidean distance
            Assert.That(graph.TryConnect(a, b, double.PositiveInfinity), Is.False);
            Assert.That(graph.TryConnect(a, 100, 2), Is.False);
            Assert.That(graph.TryConnect(a, b, 2), Is.True);
            Assert.That(graph.TryConnect(a, b, 2), Is.False); // duplicate
        }

        [Test]
        public void TriangulationConnectsOnlySharedWalkableEdges()
        {
            var vertices = new[] { P(0, 0), P(1, 0), P(0, 1), P(1, 1), P(5, 0), P(6, 0), P(5, 1) };
            int[] triangles = { 0, 1, 2, 1, 3, 2, 4, 5, 6 };
            HunterNavGraph graph = HunterTriangulationGraphBuilder.Build(vertices, triangles);
            Assert.That(graph.NodeCount, Is.EqualTo(3));
            Assert.That(graph.GetEdges(0).Count, Is.EqualTo(1));
            Assert.That(graph.GetEdges(1).Count, Is.EqualTo(1));
            Assert.That(graph.GetEdges(2).Count, Is.Zero);
            Assert.That(HunterPathSearch.AStar(graph, 0, 1).Succeeded, Is.True);
            Assert.That(HunterPathSearch.AStar(graph, 0, 2).Succeeded, Is.False);
            Assert.That(HunterTriangulationGraphBuilder.Build(vertices, triangles, (a, b) => false).GetEdges(0).Count, Is.Zero);
        }

        [Test]
        public void MalformedTriangulationProducesSafeEmptyOrPartialGraph()
        {
            Assert.That(HunterTriangulationGraphBuilder.Build(null, null).NodeCount, Is.Zero);
            var vertices = new[] { P(0, 0), P(1, 0), P(0, 1) };
            Assert.That(HunterTriangulationGraphBuilder.Build(vertices, new[] { 0, 1, 99 }).NodeCount, Is.Zero);
            Assert.That(HunterTriangulationGraphBuilder.Build(vertices, new[] { 0, 0, 1 }).NodeCount, Is.Zero);
            Assert.That(HunterTriangulationGraphBuilder.Build(vertices, new[] { 0, 1 }).NodeCount, Is.Zero);
        }

        [Test]
        public void PriorityQueueOrdersByFThenHThenNodeAndHandlesEmpty()
        {
            var queue = new HunterPriorityQueue();
            Assert.That(queue.TryDequeue(out _), Is.False);
            Assert.That(queue.Enqueue(2, 2, 1), Is.True); // f=3, h=1
            Assert.That(queue.Enqueue(1, 2, 1), Is.True); // same f/h, lower id
            Assert.That(queue.Enqueue(3, 1, 1), Is.True); // f=2
            Assert.That(queue.Enqueue(4, 2.5, 0.5), Is.True); // f=3, lower h
            Assert.That(queue.Enqueue(5, double.NaN, 0), Is.False);
            Assert.That(queue.Count, Is.EqualTo(4));
            Assert.That(queue.TryDequeue(out HunterPriorityQueue.Entry first) && first.Node == 3, Is.True);
            Assert.That(queue.TryDequeue(out HunterPriorityQueue.Entry second) && second.Node == 4, Is.True);
            Assert.That(queue.TryDequeue(out HunterPriorityQueue.Entry third) && third.Node == 1, Is.True);
            Assert.That(queue.TryDequeue(out HunterPriorityQueue.Entry fourth) && fourth.Node == 2, Is.True);
            Assert.That(queue.TryDequeue(out _), Is.False);
        }
    }
}
