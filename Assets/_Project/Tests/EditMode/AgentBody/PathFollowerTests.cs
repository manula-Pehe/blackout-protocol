using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BlackoutProtocol.Body.Tests
{
    public sealed class PathFollowerTests
    {
        private const float Acceptance = 0.4f;
        private const float Arrival = 0.15f;

        [Test]
        public void NewFollower_HasNoPathAndIsFinished()
        {
            var follower = new PathFollower();

            Assert.IsFalse(follower.HasPath);
            Assert.IsTrue(follower.IsFinished);
            Assert.AreEqual(0f, follower.RemainingDistance(Vector3.zero));
        }

        [Test]
        public void SetPath_CopiesWaypoints()
        {
            var source = new List<Vector3> { Vector3.zero, new Vector3(5f, 0f, 0f) };
            var follower = new PathFollower();

            follower.SetPath(source);
            source.Clear();
            source.Add(new Vector3(99f, 0f, 99f));

            Assert.AreEqual(2, follower.Waypoints.Count);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), follower.Destination);
        }

        [Test]
        public void Advance_SkipsWaypointsAlreadyReached()
        {
            var follower = new PathFollower();
            follower.SetPath(new[]
            {
                Vector3.zero,
                new Vector3(0.2f, 0f, 0f),
                new Vector3(5f, 0f, 0f)
            });

            bool finished = follower.Advance(Vector3.zero, Acceptance, Arrival);

            Assert.IsFalse(finished);
            Assert.AreEqual(2, follower.CurrentIndex);
        }

        [Test]
        public void Advance_LastWaypointUsesTighterArrivalRadius()
        {
            var follower = new PathFollower();
            follower.SetPath(new[] { new Vector3(5f, 0f, 0f) });

            Assert.IsFalse(follower.Advance(new Vector3(4.7f, 0f, 0f), Acceptance, Arrival));
            Assert.IsTrue(follower.Advance(new Vector3(4.9f, 0f, 0f), Acceptance, Arrival));
        }

        [Test]
        public void Advance_IgnoresHeightDifference()
        {
            var follower = new PathFollower();
            follower.SetPath(new[] { new Vector3(0f, 0.9f, 0f) });

            Assert.IsTrue(follower.Advance(Vector3.zero, Acceptance, Arrival));
        }

        [Test]
        public void RemainingDistance_SumsRemainingSegments()
        {
            var follower = new PathFollower();
            follower.SetPath(new[]
            {
                new Vector3(3f, 0f, 0f),
                new Vector3(3f, 0f, 4f)
            });

            Assert.AreEqual(7f, follower.RemainingDistance(Vector3.zero), 0.0001f);
        }

        [Test]
        public void Clear_RemovesPath()
        {
            var follower = new PathFollower();
            follower.SetPath(new[] { Vector3.one });

            follower.Clear();

            Assert.IsFalse(follower.HasPath);
            Assert.IsTrue(follower.IsFinished);
        }
    }
}
