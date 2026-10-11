using System;
using BlackoutProtocol.Core;
using NUnit.Framework;
using UnityEngine;

namespace BlackoutProtocol.Core.Tests
{
    public sealed class CoreContractsTests
    {
        [Test]
        public void MoveCommand_WithNullPath_HasEmptyPath()
        {
            var command = new MoveCommand(null, MoveStyle.Walk);

            Assert.That(command.Path, Is.Not.Null);
            Assert.That(command.HasPath, Is.False);
            Assert.That(command.Destination, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MoveCommand_Destination_IsLastWaypoint()
        {
            var path = new[] { Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 2f) };

            var command = new MoveCommand(path, MoveStyle.Run);

            Assert.That(command.HasPath, Is.True);
            Assert.That(command.Destination, Is.EqualTo(new Vector3(1f, 0f, 2f)));
        }

        [Test]
        public void MoveCommand_Hold_IsIdleWithoutPath()
        {
            MoveCommand command = MoveCommand.Hold();

            Assert.That(command.Style, Is.EqualTo(MoveStyle.Idle));
            Assert.That(command.HasPath, Is.False);
        }

        [Test]
        public void NoiseEvent_NegativeLoudness_IsClampedToZero()
        {
            var noise = new NoiseEvent(Vector3.zero, -5f, NoiseType.Footstep, 0f);

            Assert.That(noise.Loudness, Is.EqualTo(0f));
        }

        [Test]
        public void GameEvents_RaiseNoise_NotifiesSubscribers()
        {
            NoiseEvent? received = null;
            Action<NoiseEvent> handler = noise => received = noise;
            GameEvents.Noise += handler;

            try
            {
                GameEvents.RaiseNoise(new NoiseEvent(Vector3.one, NoiseLevels.Gunshot, NoiseType.Gunshot, 1f));
            }
            finally
            {
                GameEvents.Noise -= handler;
            }

            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.Type, Is.EqualTo(NoiseType.Gunshot));
            Assert.That(received.Value.Loudness, Is.EqualTo(NoiseLevels.Gunshot));
        }

        [Test]
        public void GameLayers_SightBlockers_ExcludeAgentsAndPlayer()
        {
            int mask = GameLayers.SightBlockers;

            Assert.That(mask & (1 << GameLayers.Player), Is.EqualTo(0));
            Assert.That(mask & (1 << GameLayers.Enemy), Is.EqualTo(0));
            Assert.That(mask & (1 << GameLayers.Obstacle), Is.Not.EqualTo(0));
        }
    }
}
