using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BlackoutProtocol.AI.Sentinel.Tests
{
    public sealed class SentinelGraphSearchTests
    {
        [Test]
        public void AStarAndUniformCost_ReturnEqualRouteCostAndAStarExpandsNoMoreOnStraightGoalGraph()
        {
            Vector3[] positions =
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f)
            };
            SentinelGraphEdge[][] edges =
            {
                new[] { new SentinelGraphEdge(1, 1f), new SentinelGraphEdge(3, 1f), new SentinelGraphEdge(4, 1f) },
                new[] { new SentinelGraphEdge(0, 1f), new SentinelGraphEdge(2, 1f) },
                new[] { new SentinelGraphEdge(1, 1f) },
                new[] { new SentinelGraphEdge(0, 1f) },
                new[] { new SentinelGraphEdge(0, 1f) }
            };
            var search = new SentinelGraphSearch(positions, edges);
            var path = new List<int>();
            SentinelPathSearchStats aStar = search.Search(0, 2, true, path);
            Assert.That(path, Is.EqualTo(new[] { 0, 1, 2 }));
            SentinelPathSearchStats ucs = search.Search(0, 2, false, path);
            Assert.That(aStar.Succeeded && ucs.Succeeded, Is.True);
            Assert.That(aStar.TotalCost, Is.EqualTo(ucs.TotalCost).Within(0.0001f));
            Assert.That(aStar.NodesExpanded, Is.LessThanOrEqualTo(ucs.NodesExpanded));
        }

        [Test]
        public void AStar_StartEqualsGoal_ReturnsOneNodeAtZeroCost()
        {
            var search = new SentinelGraphSearch(
                new[] { Vector3.zero },
                new[] { System.Array.Empty<SentinelGraphEdge>() });
            var path = new List<int>();
            SentinelPathSearchStats result = search.Search(0, 0, true, path);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(path, Is.EqualTo(new[] { 0 }));
            Assert.That(result.TotalCost, Is.Zero);
            Assert.That(result.NodesExpanded, Is.EqualTo(1));
        }

        [Test]
        public void GraphSearch_ReturnsFailureForDisconnectedGoal()
        {
            var search = new SentinelGraphSearch(
                new[] { Vector3.zero, Vector3.right },
                new[] { System.Array.Empty<SentinelGraphEdge>(), System.Array.Empty<SentinelGraphEdge>() });
            var path = new List<int>();
            Assert.That(search.Search(0, 1, true, path).Succeeded, Is.False);
            Assert.That(path, Is.Empty);
        }
    }
}
