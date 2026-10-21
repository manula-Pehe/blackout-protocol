using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BlackoutProtocol.AI.Sentinel.Tests
{
    public sealed class SentinelDecisionLogicTests
    {
        [Test]
        public void PatrolSelection_PrefersLeastRecentlyVisitedReachableWaypointAfterTravelPenalty()
        {
            var options = new[]
            {
                new SentinelDecisionLogic.PatrolOption(0, 1f, 2f, true),
                new SentinelDecisionLogic.PatrolOption(1, 0f, 1f, true),
                new SentinelDecisionLogic.PatrolOption(2, -100f, 1f, false)
            };
            Assert.That(SentinelDecisionLogic.ScorePatrol(20f, 1f, 2f), Is.EqualTo(18.6f).Within(0.001f));
            Assert.That(SentinelDecisionLogic.SelectPatrol(options, -1, false), Is.EqualTo(1));
        }

        [Test]
        public void PatrolSelection_SkipsCurrentWaypointWhenAlternativesExist()
        {
            var options = new[]
            {
                new SentinelDecisionLogic.PatrolOption(0, -100f, 1f, true),
                new SentinelDecisionLogic.PatrolOption(1, 0f, 1f, true)
            };
            Assert.That(SentinelDecisionLogic.SelectPatrol(options, 0, true), Is.EqualTo(1));
        }

        [Test]
        public void SearchPrediction_RanksForwardPredictionBeforeFlanksAndLastSeenPosition()
        {
            var options = new List<SentinelDecisionLogic.SearchOption>();
            SentinelDecisionLogic.BuildSearchOptions(new Vector3(2f, 0f, 3f), Vector3.forward, 2f, 3f, options);
            Assert.That(options, Has.Count.EqualTo(5));
            Assert.That(options[0].Position, Is.EqualTo(new Vector3(2f, 0f, 5f)));
            Assert.That(options[0].Score, Is.GreaterThan(options[1].Score));
            Assert.That(options[1].Position, Is.EqualTo(new Vector3(5f, 0f, 5f)));
            Assert.That(options[2].Position, Is.EqualTo(new Vector3(-1f, 0f, 5f)));
            Assert.That(options[4].Position, Is.EqualTo(new Vector3(2f, 0f, 3f)));
        }
    }
}
