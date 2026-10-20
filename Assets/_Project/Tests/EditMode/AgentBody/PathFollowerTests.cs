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
        public void SetPath_FromOwnReadOnlyView_DoesNotEraseReplacement()
        {
            var follower = new PathFollower();
            follower.SetPath(new[] { Vector3.zero, Vector3.forward * 2 });
            follower.Advance(Vector3.zero, Acceptance, Arrival);
            follower.SetPath(follower.Waypoints);
            Assert.AreEqual(2, follower.Waypoints.Count);
            Assert.AreEqual(Vector3.forward * 2, follower.Destination);
            Assert.AreEqual(0, follower.CurrentIndex);
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

        [Test]
        public void Bend_RequiresTightRadiusAndDoesNotJumpToLaterSegment()
        {
            var follower = new PathFollower();
            follower.SetPath(new[] { Vector3.zero, new Vector3(0, 0, 2), new Vector3(2, 0, 2) });
            follower.Advance(Vector3.zero, Acceptance, Arrival, 0.08f);
            Assert.IsTrue(follower.IsCurrentCorner);
            follower.Advance(new Vector3(0, 0, 1.7f), Acceptance, Arrival, 0.08f);
            Assert.AreEqual(1, follower.CurrentIndex);
            follower.Advance(new Vector3(0, 0, 1.93f), Acceptance, Arrival, 0.08f);
            Assert.AreEqual(2, follower.CurrentIndex);
            Assert.IsFalse(follower.IsFinished);
        }

        [Test]
        public void ShortBend_RadiusIsBoundedByAdjacentSegments()
        {
            var follower = new PathFollower();
            follower.SetPath(new[] { Vector3.zero, Vector3.forward * 0.1f, new Vector3(0.1f, 0, 0.1f) });
            follower.Advance(Vector3.zero, Acceptance, 0.01f, 0.08f);
            Assert.AreEqual(1, follower.CurrentIndex);
            Assert.AreEqual(0.025f, follower.CurrentAcceptanceRadius(Acceptance, Arrival, 0.08f), 0.0001f);
        }

        [Test]
        public void RemainingRoute_DoesNotUseStraightLineAcrossCorner()
        {
            var follower = new PathFollower();
            follower.SetPath(new[] { new Vector3(0, 0, 4), new Vector3(1, 0, 4), Vector3.right });
            Assert.AreEqual(9, follower.RemainingDistance(Vector3.zero), 0.0001f);
            Assert.AreEqual(4, SteeringMath.ArrivalSpeed(4, follower.RemainingDistance(Vector3.zero), 1.5f));
        }

        [Test]
        public void ZeroLengthAndDuplicateSegments_FinishWithoutLoop()
        {
            var follower = new PathFollower();
            follower.SetPath(new[] { Vector3.zero, Vector3.zero, Vector3.zero });
            Assert.IsTrue(follower.Advance(Vector3.zero, Acceptance, Arrival, 0.08f));
            Assert.AreEqual(1, follower.CurrentIndex);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidPoint_RejectsEntireRouteInsteadOfBridgingIt(float coordinate)
        {
            var follower = new PathFollower();
            follower.SetPath(new[] { Vector3.zero, new Vector3(coordinate, 0, 2), Vector3.one });
            Assert.IsFalse(follower.HasPath);
            Assert.IsTrue(follower.IsFinished);
        }

        [TestCase(0.02f)]
        [TestCase(0.2f)]
        public void ArrivalEnvelope_SlowsAndStopsWithinToleranceWithLimitedAcceleration(float dt)
        {
            float distance = 8, speed = 0, previousSpeed;
            bool slowed = false;
            for (int frame = 0; frame < 3000 && distance > Arrival; frame++)
            {
                previousSpeed = speed;
                float target = Mathf.Min(SteeringMath.ArrivalSpeed(4, distance, 1.5f),
                    SteeringMath.ApproachSpeed(distance, 0, 8, dt));
                speed = Mathf.MoveTowards(speed, target, 8 * dt);
                Assert.LessOrEqual(Mathf.Abs(speed - previousSpeed), 8 * dt + 0.0001f);
                if (distance < 1) slowed |= speed < 3;
                distance -= Mathf.Min(distance, speed * dt);
            }
            Assert.LessOrEqual(distance, Arrival);
            Assert.GreaterOrEqual(distance, 0);
            Assert.Less(speed, 0.7f);
            Assert.IsTrue(slowed);
        }

        [Test]
        public void Envelopes_PreserveCruiseOrderingAndHaveSafeZeroAndInvalidResults()
        {
            Assert.AreEqual(4, SteeringMath.ArrivalSpeed(4, 10, 1.5f));
            Assert.Greater(SteeringMath.ArrivalSpeed(4, 0.75f, 1.5f), SteeringMath.ArrivalSpeed(1.8f, 0.75f, 1.5f));
            Assert.Greater(SteeringMath.ArrivalSpeed(1.8f, 0.75f, 1.5f), SteeringMath.ArrivalSpeed(1.6f, 0.75f, 1.5f));
            Assert.Greater(SteeringMath.ArrivalSpeed(1.6f, 0.75f, 1.5f), SteeringMath.ArrivalSpeed(1, 0.75f, 1.5f));
            Assert.AreEqual(0, SteeringMath.ArrivalSpeed(4, 0, 1.5f));
            Assert.AreEqual(0, SteeringMath.ArrivalSpeed(4, float.NaN, 1.5f));
            Assert.AreEqual(0, SteeringMath.ApproachSpeed(float.PositiveInfinity, 0, 8, 0.02f));
            Assert.AreEqual(0.5f, SteeringMath.ApproachSpeed(0, 0.5f, 8, 0.02f), 0.0001f);
        }
    }
}
