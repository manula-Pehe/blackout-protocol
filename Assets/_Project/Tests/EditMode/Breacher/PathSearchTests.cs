using BlackoutProtocol.AI.Breacher.Logic;
using NUnit.Framework;
using UnityEngine;

namespace BlackoutProtocol.AI.Breacher.Tests
{
    public sealed class PathSearchTests
    {
        [Test]
        public void AStar_FindsCheapestPath()
        {
            var graph = new NavGraph();
            NavNode start = graph.AddNode(new Vector3(0f, 0f, 0f));
            NavNode preferred = graph.AddNode(new Vector3(1f, 0f, 0f));
            NavNode expensive = graph.AddNode(new Vector3(0f, 0f, 1f));
            NavNode goal = graph.AddNode(new Vector3(2f, 0f, 0f));

            graph.ConnectBidirectional(start, preferred, 1f);
            graph.ConnectBidirectional(preferred, goal, 1f);
            graph.ConnectBidirectional(start, expensive, 1f);
            graph.ConnectBidirectional(expensive, goal, 5f);

            PathSearchResult result =
                PathSearch.FindWithAStar(start, goal);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.TotalCost, Is.EqualTo(2f).Within(0.001f));
            CollectionAssert.AreEqual(
                new[] { start, preferred, goal },
                result.Path);
        }

        [Test]
        public void AStar_ReturnsFailureWhenGoalIsUnreachable()
        {
            var graph = new NavGraph();
            NavNode start = graph.AddNode(Vector3.zero);
            NavNode reachable = graph.AddNode(Vector3.right);
            NavNode isolatedGoal = graph.AddNode(Vector3.forward * 5f);

            graph.ConnectBidirectional(start, reachable, 1f);

            PathSearchResult result =
                PathSearch.FindWithAStar(start, isolatedGoal);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Path, Is.Empty);
            Assert.That(float.IsPositiveInfinity(result.TotalCost), Is.True);
        }

        [Test]
        public void AStar_ReturnsStartWhenStartEqualsGoal()
        {
            var graph = new NavGraph();
            NavNode start = graph.AddNode(Vector3.zero);

            PathSearchResult result =
                PathSearch.FindWithAStar(start, start);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.TotalCost, Is.EqualTo(0f));
            CollectionAssert.AreEqual(new[] { start }, result.Path);
        }

        [Test]
        public void AStarAndUcs_ReturnSameCost_ButAStarExpandsFewerNodes()
        {
            var graph = new NavGraph();
            NavNode start = graph.AddNode(new Vector3(0f, 0f, 0f));
            NavNode goal = graph.AddNode(new Vector3(4f, 0f, 0f));

            NavNode distractionOne =
                graph.AddNode(new Vector3(0f, 0f, 1f));
            NavNode distractionTwo =
                graph.AddNode(new Vector3(0f, 0f, 2f));
            NavNode distractionThree =
                graph.AddNode(new Vector3(0f, 0f, 3f));

            graph.ConnectBidirectional(start, distractionOne, 1f);
            graph.ConnectBidirectional(distractionOne, distractionTwo, 1f);
            graph.ConnectBidirectional(distractionTwo, distractionThree, 1f);

            NavNode routeOne =
                graph.AddNode(new Vector3(1f, 0f, 0f));
            NavNode routeTwo =
                graph.AddNode(new Vector3(2f, 0f, 0f));
            NavNode routeThree =
                graph.AddNode(new Vector3(3f, 0f, 0f));

            graph.ConnectBidirectional(start, routeOne, 1f);
            graph.ConnectBidirectional(routeOne, routeTwo, 1f);
            graph.ConnectBidirectional(routeTwo, routeThree, 1f);
            graph.ConnectBidirectional(routeThree, goal, 1f);

            PathSearchResult aStar =
                PathSearch.FindWithAStar(start, goal);
            PathSearchResult ucs =
                PathSearch.FindWithUcs(start, goal);

            Assert.That(aStar.Succeeded, Is.True);
            Assert.That(ucs.Succeeded, Is.True);
            Assert.That(aStar.TotalCost,
                Is.EqualTo(ucs.TotalCost).Within(0.001f));
            Assert.That(aStar.ExpandedNodeCount,
                Is.LessThan(ucs.ExpandedNodeCount));
        }
    }
}
