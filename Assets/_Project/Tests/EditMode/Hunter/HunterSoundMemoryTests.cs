using BlackoutProtocol.AI.Hunter.Logic;
using BlackoutProtocol.Core;
using NUnit.Framework;

namespace BlackoutProtocol.AI.Hunter.Tests
{
    public sealed class HunterSoundMemoryTests
    {
        [Test]
        public void CredibleSound_IsRememberedWithDebugValues()
        {
            var memory = new HunterSoundMemory();
            var sound = Sound(NoiseType.Gunshot, 10d, NoiseLevels.Gunshot, 0.8f);

            Assert.That(memory.Record(sound, 10d), Is.True);
            Assert.That(memory.TryGetMostRelevant(10d, out HunterRememberedNoise result),
                Is.True);
            Assert.That(result.Observation.Category, Is.EqualTo(NoiseType.Gunshot));
            Assert.That(result.Confidence, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(result.SoundIntensity, Is.EqualTo(1f));
            Assert.That(result.EffectiveTrust, Is.EqualTo(1f));
            Assert.That(result.Priority, Is.GreaterThan(0f));
        }

        [Test]
        public void Confidence_HalvesAfterConfiguredHalfLife()
        {
            var memory = new HunterSoundMemory(confidenceHalfLife: 4d);
            memory.Record(Sound(NoiseType.Gunshot, 0d, NoiseLevels.Gunshot, 0.8f), 0d);

            Assert.That(memory.TryGetMostRelevant(4d, out HunterRememberedNoise result),
                Is.True);
            Assert.That(result.Confidence, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(result.Recency, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void StrongRecentSound_OutranksWeakOlderSound()
        {
            var memory = new HunterSoundMemory();
            memory.Record(Sound(NoiseType.Gunshot, 0d, 3f, 0.5f, x: 1f), 0d);
            memory.Record(Sound(NoiseType.Gunshot, 5d, 30f, 1f, x: 2f), 5d);

            Assert.That(memory.TryGetMostRelevant(5d, out HunterRememberedNoise result),
                Is.True);
            Assert.That(result.Observation.X, Is.EqualTo(2f));
        }

        [Test]
        public void ExpiredObservations_AreRemoved()
        {
            var memory = new HunterSoundMemory(maximumAge: 3d);
            memory.Record(Sound(NoiseType.Door, 0d, NoiseLevels.Door), 0d);

            Assert.That(memory.TryGetMostRelevant(4d, out _), Is.False);
            Assert.That(memory.Count, Is.Zero);
        }

        [Test]
        public void RepeatedNearbyImpacts_ReduceOnlyImpactTrust()
        {
            var memory = new HunterSoundMemory();
            memory.Record(Sound(NoiseType.Impact, 0d, NoiseLevels.Impact), 0d);
            memory.Record(Sound(NoiseType.Impact, 1d, NoiseLevels.Impact, x: 1f), 1d);

            Assert.That(memory.TryGetMostRelevant(1d, out HunterRememberedNoise result),
                Is.True);
            Assert.That(result.EffectiveTrust, Is.LessThan(1f));
            Assert.That(result.EffectiveTrust, Is.GreaterThanOrEqualTo(0.2f));
        }

        [Test]
        public void DistantImpacts_DoNotShareHabituation()
        {
            var memory = new HunterSoundMemory(impactRadius: 2f);
            memory.Record(Sound(NoiseType.Impact, 0d, NoiseLevels.Impact, x: 0f), 0d);
            memory.Record(Sound(NoiseType.Impact, 1d, NoiseLevels.Impact, x: 10f), 1d);

            Assert.That(memory.TryGetMostRelevant(1d, out HunterRememberedNoise result),
                Is.True);
            Assert.That(result.Observation.X, Is.EqualTo(10f));
            Assert.That(result.EffectiveTrust, Is.EqualTo(1f));
        }

        [Test]
        public void MovingImpactChain_DoesNotSpreadHabituationAcrossRoom()
        {
            var memory = new HunterSoundMemory(impactRadius: 2f);
            memory.Record(Sound(NoiseType.Impact, 0d, NoiseLevels.Impact, x: 0f), 0d);
            memory.Record(Sound(NoiseType.Impact, 1d, NoiseLevels.Impact, x: 1.5f), 1d);
            memory.Record(Sound(NoiseType.Impact, 2d, NoiseLevels.Impact, x: 3f), 2d);

            Assert.That(memory.TryGetMostRelevant(2d, out HunterRememberedNoise result),
                Is.True);
            Assert.That(result.Observation.X, Is.EqualTo(3f));
            Assert.That(result.EffectiveTrust, Is.EqualTo(1f));
        }

        [Test]
        public void RepetitiveImpacts_DoNotSuppressGunshotsOrAlarms()
        {
            var memory = new HunterSoundMemory();
            for (int i = 0; i < 5; i++)
            {
                memory.Record(Sound(NoiseType.Impact, i, NoiseLevels.Impact), i);
            }

            memory.Record(Sound(NoiseType.Gunshot, 5d, NoiseLevels.Gunshot), 5d);
            Assert.That(memory.TryGetMostRelevant(5d, out HunterRememberedNoise gunshot),
                Is.True);
            Assert.That(gunshot.Observation.Category, Is.EqualTo(NoiseType.Gunshot));
            Assert.That(gunshot.EffectiveTrust, Is.EqualTo(1f));

            memory.Record(Sound(NoiseType.Alarm, 6d, NoiseLevels.Alarm), 6d);
            Assert.That(memory.TryGetMostRelevant(6d, out HunterRememberedNoise alarm),
                Is.True);
            Assert.That(alarm.Observation.Category, Is.EqualTo(NoiseType.Alarm));
            Assert.That(alarm.EffectiveTrust, Is.EqualTo(1f));
        }

        [Test]
        public void Habituation_RecoversAfterImpactsStop()
        {
            var memory = new HunterSoundMemory(habituationHalfLife: 4d);
            memory.Record(Sound(NoiseType.Impact, 0d, NoiseLevels.Impact), 0d);
            memory.Record(Sound(NoiseType.Impact, 1d, NoiseLevels.Impact), 1d);

            memory.TryGetMostRelevant(1d, out HunterRememberedNoise recent);
            memory.TryGetMostRelevant(5d, out HunterRememberedNoise recovered);

            Assert.That(recovered.EffectiveTrust, Is.GreaterThan(recent.EffectiveTrust));
            Assert.That(1f - recovered.EffectiveTrust,
                Is.EqualTo((1f - recent.EffectiveTrust) * 0.5f).Within(0.0001f));
        }

        [Test]
        public void EmptyMemory_ReturnsSafeNoTarget()
        {
            var memory = new HunterSoundMemory();

            Assert.That(memory.TryGetMostRelevant(0d, out HunterRememberedNoise result),
                Is.False);
            Assert.That(result.Priority, Is.Zero);
            Assert.That(memory.Count, Is.Zero);
        }

        [Test]
        public void InvalidObservationsAndTimes_AreRejectedWithoutChangingMemory()
        {
            var memory = new HunterSoundMemory();
            Assert.That(memory.Record(Sound(NoiseType.Impact, double.NaN,
                NoiseLevels.Impact), 1d), Is.False);
            Assert.That(memory.Record(Sound(NoiseType.Impact, 2d,
                NoiseLevels.Impact), 1d), Is.False);
            Assert.That(memory.Record(Sound((NoiseType)999, 0d, 1f), 0d), Is.False);
            Assert.That(memory.Record(Sound(NoiseType.Impact, 0d, float.NaN), 0d),
                Is.False);
            Assert.That(memory.Record(Sound(NoiseType.Impact, 0d, 1f,
                x: float.PositiveInfinity), 0d), Is.False);
            Assert.That(memory.Record(Sound(NoiseType.Impact, 0d, 1f),
                double.PositiveInfinity), Is.False);
            Assert.That(memory.TryGetMostRelevant(double.NaN, out _), Is.False);
            Assert.That(memory.Count, Is.Zero);
        }

        [Test]
        public void ExtremeFiniteTime_ProducesFiniteScores()
        {
            var memory = new HunterSoundMemory();
            memory.Record(Sound(NoiseType.Alarm, 1e300d, NoiseLevels.Alarm),
                1e300d);

            Assert.That(memory.TryGetMostRelevant(1e300d,
                out HunterRememberedNoise result), Is.True);
            Assert.That(float.IsNaN(result.Priority) || float.IsInfinity(result.Priority),
                Is.False);
            Assert.That(float.IsNaN(result.Confidence) ||
                float.IsInfinity(result.Confidence), Is.False);
            Assert.That(memory.TryGetMostRelevant(double.MaxValue, out _), Is.False);
        }

        [Test]
        public void Capacity_RemainsBoundedUnderManyObservations()
        {
            var memory = new HunterSoundMemory(capacity: 3);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(memory.Record(Sound(NoiseType.Gunshot, i,
                    NoiseLevels.Gunshot, x: i), i), Is.True);
                Assert.That(memory.Count, Is.LessThanOrEqualTo(3));
            }

            Assert.That(memory.Capacity, Is.EqualTo(3));
            Assert.That(memory.Count, Is.EqualTo(3));
            Assert.That(new HunterSoundMemory(capacity: int.MaxValue).Capacity,
                Is.EqualTo(HunterSoundMemory.MaximumCapacity));
        }

        [Test]
        public void FullMemory_KeepsMoreRelevantSound()
        {
            var memory = new HunterSoundMemory(capacity: 1);
            memory.Record(Sound(NoiseType.Alarm, 0d, NoiseLevels.Alarm), 0d);

            Assert.That(memory.Record(Sound(NoiseType.Impact, 0d, 1f), 0d),
                Is.False);
            Assert.That(memory.TryGetMostRelevant(0d, out HunterRememberedNoise result),
                Is.True);
            Assert.That(result.Observation.Category, Is.EqualTo(NoiseType.Alarm));
            Assert.That(memory.Count, Is.EqualTo(1));
        }

        [Test]
        public void EqualPriority_PrefersNewerObservation()
        {
            var memory = new HunterSoundMemory();
            memory.Record(Sound(NoiseType.Gunshot, 0d, NoiseLevels.Gunshot,
                x: 1f), 0d);
            memory.Record(Sound(NoiseType.Gunshot, 6d, NoiseLevels.Gunshot,
                confidence: 0.25f, x: 2f), 6d);

            Assert.That(memory.TryGetMostRelevant(6d, out HunterRememberedNoise result),
                Is.True);
            Assert.That(result.Observation.X, Is.EqualTo(2f));
        }

        [Test]
        public void EqualPriorityAndTime_PrefersEarlierInsertion()
        {
            var memory = new HunterSoundMemory();
            memory.Record(Sound(NoiseType.Gunshot, 0d, NoiseLevels.Gunshot,
                x: 1f), 0d);
            memory.Record(Sound(NoiseType.Gunshot, 0d, NoiseLevels.Gunshot,
                x: 2f), 0d);

            Assert.That(memory.TryGetMostRelevant(0d, out HunterRememberedNoise result),
                Is.True);
            Assert.That(result.Observation.X, Is.EqualTo(1f));
        }

        private static HunterNoiseObservation Sound(
            NoiseType category,
            double time,
            float loudness,
            float confidence = 1f,
            float x = 0f)
        {
            return new HunterNoiseObservation(
                x, 0f, 0f, category, time, loudness, confidence);
        }
    }
}
