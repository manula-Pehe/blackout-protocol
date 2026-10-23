using System;
using System.Collections.Generic;
using BlackoutProtocol.AI.Flanker.Logic;
using NUnit.Framework;

namespace BlackoutProtocol.AI.Flanker.Tests
{
    public sealed class FlankerNavigationTests
    {
        private static FlankerGraph TwoRoutes()
        {
            var graph = new FlankerGraph(new[] { new FlankerPoint(0f, 0f, 0f), new FlankerPoint(1f, 0f, 0f),
                new FlankerPoint(0f, 0f, 2f), new FlankerPoint(2f, 0f, 0f) });
            graph.Connect(0, 1);
            graph.Connect(1, 3);
            graph.Connect(0, 2);
            graph.Connect(2, 3);
            return graph;
        }

        [Test]
        public void ExposureSelectsTheLongerConcealedRoute()
        {
            var search = new FlankerAStar(TwoRoutes());
            var path = new List<int>();
            var exposure = new[] { 0f, 1f, 0f, 0f };
            Assert.That(search.Find(0, 3, exposure, 0f, path, out double directCost), Is.True);
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, path);
            Assert.That(directCost, Is.EqualTo(2d));
            Assert.That(search.Find(0, 3, exposure, 5f, path, out _), Is.True);
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, path);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(5f)]
        public void AdmissibleAStarMatchesUniformCost(float penalty)
        {
            FlankerGraph graph = TwoRoutes();
            var search = new FlankerAStar(graph);
            var path = new List<int>();
            var exposure = new[] { 0.2f, 1f, 0f, 0.4f };
            Assert.That(search.Find(0, 3, exposure, penalty, path, out double aStarCost), Is.True);
            Assert.That(search.Find(0, 3, exposure, penalty, path, out double uniformCost, heuristic: false), Is.True);
            Assert.That(aStarCost, Is.EqualTo(uniformCost).Within(0.000001d));
            Assert.That(aStarCost, Is.GreaterThanOrEqualTo(FlankerPoint.Distance(graph.Point(0), graph.Point(3))));
        }

        [Test]
        public void SeededWeightedGraphsMatchUniformCostForEveryEndpointPair()
        {
            var random = new Random(3032);
            for (int sample = 0; sample < 12; sample++)
            {
                var points = new FlankerPoint[8];
                var exposure = new float[8];
                for (int index = 0; index < points.Length; index++)
                {
                    points[index] = new FlankerPoint(index, 0f, (float)random.NextDouble() * 5f);
                    exposure[index] = (float)random.NextDouble();
                }
                var graph = new FlankerGraph(points);
                for (int index = 1; index < points.Length; index++) graph.Connect(index - 1, index);
                for (int extra = 0; extra < 10; extra++) graph.Connect(random.Next(8), random.Next(8));
                var search = new FlankerAStar(graph);
                var path = new List<int>();
                for (int start = 0; start < points.Length; start++)
                {
                    for (int goal = 0; goal < points.Length; goal++)
                    {
                        Assert.That(search.Find(start, goal, exposure, 5f, path, out double aStar), Is.True);
                        Assert.That(search.Find(start, goal, exposure, 5f, path, out double uniform, heuristic: false), Is.True);
                        Assert.That(aStar, Is.EqualTo(uniform).Within(0.000001d));
                    }
                }
            }
        }

        [Test]
        public void SearchFailureClearsOldRouteAndHonoursBudget()
        {
            var graph = new FlankerGraph(new[] { new FlankerPoint(0f, 0f, 0f), new FlankerPoint(1f, 0f, 0f), new FlankerPoint(2f, 0f, 0f) });
            graph.Connect(0, 1);
            var search = new FlankerAStar(graph);
            var path = new List<int> { 42 };
            Assert.That(search.Find(0, 2, new float[3], 1f, path, out double cost), Is.False);
            Assert.That(path, Is.Empty);
            Assert.That(cost, Is.EqualTo(double.PositiveInfinity));
            graph.Connect(1, 2);
            Assert.That(search.Find(0, 2, new float[3], 1f, path, out _, maximumExpanded: 1), Is.False);
            Assert.That(search.Expanded, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void SameNodeIsAValidOnePointPath()
        {
            var search = new FlankerAStar(TwoRoutes());
            var path = new List<int>();
            Assert.That(search.Find(1, 1, new float[4], 1f, path, out double cost), Is.True);
            CollectionAssert.AreEqual(new[] { 1 }, path);
            Assert.That(cost, Is.Zero);
        }

        [Test]
        public void SearchRejectsNegativeOrNonfinitePenalties()
        {
            var search = new FlankerAStar(TwoRoutes());
            Assert.Throws<ArgumentOutOfRangeException>(() => search.Find(0, 3, new float[4], -1f, new List<int>(), out _));
            Assert.Throws<ArgumentException>(() => search.Find(0, 3, new[] { 0f, float.NaN, 0f, 0f }, 1f, new List<int>(), out _));
        }

        [Test]
        public void TriangulationWeldsDuplicateVerticesAcrossFullEdges()
        {
            var vertices = new[] { new FlankerPoint(0f, 0f, 0f), new FlankerPoint(2f, 0f, 0f), new FlankerPoint(0f, 0f, 2f),
                new FlankerPoint(2f, 0f, 0f), new FlankerPoint(2f, 0f, 2f), new FlankerPoint(0f, 0f, 2f) };
            FlankerGraph graph = FlankerTriangulation.Build(vertices, new[] { 0, 1, 2, 3, 4, 5 });
            Assert.That(graph.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 1 }, graph.Neighbours(0));
            Assert.That(FlankerTriangulation.Build(vertices, new[] { 0, 1, 2, 3, 4, 5 }, (first, second) => false).Neighbours(0), Is.Empty);
        }

        [Test]
        public void VertexContactAndDegenerateTrianglesDoNotCreateConnections()
        {
            var vertices = new[] { new FlankerPoint(0f, 0f, 0f), new FlankerPoint(2f, 0f, 0f), new FlankerPoint(0f, 0f, 2f),
                new FlankerPoint(-2f, 0f, 0f), new FlankerPoint(0f, 0f, -2f) };
            FlankerGraph graph = FlankerTriangulation.Build(vertices, new[] { 0, 1, 2, 0, 3, 4, 0, 0, 1 });
            Assert.That(graph.Count, Is.EqualTo(2));
            Assert.That(graph.Neighbours(0), Is.Empty);
        }

        [Test]
        public void NonManifoldEdgeIsNotConnected()
        {
            var vertices = new[] { new FlankerPoint(0f, 0f, 0f), new FlankerPoint(2f, 0f, 0f), new FlankerPoint(0f, 0f, 2f),
                new FlankerPoint(0f, 0f, -2f), new FlankerPoint(1f, 1f, 1f) };
            FlankerGraph graph = FlankerTriangulation.Build(vertices, new[] { 0, 1, 2, 1, 0, 3, 0, 1, 4 });
            for (int index = 0; index < graph.Count; index++) Assert.That(graph.Neighbours(index), Is.Empty);
        }

        [Test]
        public void ConcaveCentroidConnectionUsesTheSharedPortalInsteadOfDisconnecting()
        {
            var vertices = new[] { new FlankerPoint(0f, 0f, 0f), new FlankerPoint(1f, 0f, 0f),
                new FlankerPoint(100f, 0f, 1f), new FlankerPoint(100f, 0f, -1f) };
            FlankerGraph graph = FlankerTriangulation.Build(vertices, new[] { 0, 1, 2, 1, 0, 3 },
                (first, second) => Math.Abs(first.Z) < 0.0001f || Math.Abs(second.Z) < 0.0001f);
            Assert.That(graph.Count, Is.EqualTo(3));
            var path = new List<int>();
            Assert.That(new FlankerAStar(graph).Find(0, 1, new float[3], 1f, path, out _), Is.True);
            CollectionAssert.AreEqual(new[] { 0, 2, 1 }, path);
        }
    }
}
