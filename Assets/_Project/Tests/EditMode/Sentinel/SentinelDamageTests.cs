using BlackoutProtocol.Core;
using NUnit.Framework;
using UnityEngine;

namespace BlackoutProtocol.AI.Sentinel.Tests
{
    public sealed class SentinelDamageTests
    {
        [Test]
        public void LethalDamageStopsBrainAndLetsDroneFallUnderGravity()
        {
            var droneObject = new GameObject("Sentinel damage test");
            try
            {
                droneObject.AddComponent<BoxCollider>();
                SentinelDroneBody body = droneObject.AddComponent<SentinelDroneBody>();
                SentinelDroneBrain brain = droneObject.AddComponent<SentinelDroneBrain>();
                body.Execute(new MoveCommand(new[] { new Vector3(5f, 2f, 0f) }, MoveStyle.Walk));
                Assert.That(body.IsMoving, Is.True);

                body.TakeDamage(100f, Vector3.up, null);

                Assert.That(body.IsShotDown, Is.True);
                Assert.That(body.CurrentHealth, Is.Zero);
                Assert.That(body.IsMoving, Is.False);
                Assert.That(brain.enabled, Is.False);
                Rigidbody flightBody = droneObject.GetComponent<Rigidbody>();
                Assert.That(flightBody, Is.Not.Null);
                Assert.That(flightBody.useGravity, Is.True);
                Assert.That(flightBody.isKinematic, Is.False);

                body.TakeDamage(100f, Vector3.up, null);
                Assert.That(body.CurrentHealth, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(droneObject);
            }
        }
    }
}
