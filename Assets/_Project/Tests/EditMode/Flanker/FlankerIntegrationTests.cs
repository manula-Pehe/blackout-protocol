using System;
using System.Reflection;
using BlackoutProtocol.AI.Flanker.Logic;
using BlackoutProtocol.Core;
using NUnit.Framework;
using UnityEngine;

namespace BlackoutProtocol.AI.Flanker.Tests
{
    public sealed class FlankerIntegrationTests
    {
        private sealed class Body : IAgentBody
        {
            public event Action Arrived { add { } remove { } }
            public event Action Stuck { add { } remove { } }
            public Vector3 Position => Vector3.zero;
            public Vector3 Velocity => Vector3.zero;
            public bool IsMoving => false;
            public bool HasArrived => false;
            public bool Stopped { get; private set; }
            public AgentAction LastAction { get; private set; }
            public void Execute(MoveCommand command) { }
            public void Stop() => Stopped = true;
            public void PlayAction(AgentAction action) => LastAction = action;
        }

        [Test]
        public void DamageAdapterRaisesDeathOnlyOnce()
        {
            var instance = new GameObject("Flanker health test");
            try
            {
                FlankerHealth health = instance.AddComponent<FlankerHealth>();
                int deaths = 0;
                health.Died += () => deaths++;
                health.TakeDamage(1000f, Vector3.zero, null);
                health.TakeDamage(1000f, Vector3.zero, null);
                Assert.That(health.IsDead, Is.True);
                Assert.That(deaths, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Test]
        public void AbortingMovementReleasesActualCoverClaim()
        {
            var instance = new GameObject("Flanker abort test");
            var coverObject = new GameObject("Flanker cover test");
            instance.SetActive(false);
            try
            {
                FlankerBrain brain = instance.AddComponent<FlankerBrain>();
                CoverPoint cover = coverObject.AddComponent<CoverPoint>();
                var body = new Body();
                Assert.That(cover.TryClaim(instance), Is.True);
                typeof(FlankerBrain).GetField("claimed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(brain, cover);
                typeof(FlankerBrain).GetField("body", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(brain, body);
                brain.Abort(FlankerTactic.Flank);
                Assert.That(cover.IsFree, Is.True);
                Assert.That(brain.ClaimedCover, Is.Null);
                Assert.That(body.Stopped, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(coverObject);
            }
        }

        [Test]
        public void DeathReleasesCoverStopsBodyAndDisablesCollision()
        {
            var instance = new GameObject("Flanker death test");
            var coverObject = new GameObject("Flanker death cover");
            instance.SetActive(false);
            try
            {
                FlankerBrain brain = instance.AddComponent<FlankerBrain>();
                Collider collision = instance.AddComponent<BoxCollider>();
                CoverPoint cover = coverObject.AddComponent<CoverPoint>();
                var body = new Body();
                cover.TryClaim(instance);
                typeof(FlankerBrain).GetField("claimed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(brain, cover);
                typeof(FlankerBrain).GetField("body", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(brain, body);
                Action death = (Action)Delegate.CreateDelegate(typeof(Action), brain,
                    typeof(FlankerBrain).GetMethod("Die", BindingFlags.Instance | BindingFlags.NonPublic));
                FlankerHealth health = instance.GetComponent<FlankerHealth>();
                health.Died += death;
                health.TakeDamage(1000f, Vector3.zero, null);
                Assert.That(cover.IsFree, Is.True);
                Assert.That(body.Stopped, Is.True);
                Assert.That(body.LastAction, Is.EqualTo(AgentAction.Die));
                Assert.That(collision.enabled, Is.False);
                Assert.That(brain.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(coverObject);
            }
        }
    }
}
