using System;
using BlackoutProtocol.Core;

namespace BlackoutProtocol.AI.Hunter.Logic
{
    /// <summary>Read-only evaluation of one remembered noise at a supplied time.</summary>
    public readonly struct HunterRememberedNoise
    {
        public HunterRememberedNoise(
            HunterNoiseObservation observation,
            float soundIntensity,
            float confidence,
            float effectiveTrust,
            float recency,
            float priority)
        {
            Observation = observation;
            SoundIntensity = soundIntensity;
            Confidence = confidence;
            EffectiveTrust = effectiveTrust;
            Recency = recency;
            Priority = priority;
        }

        public HunterNoiseObservation Observation { get; }
        public float SoundIntensity { get; }
        public float Confidence { get; }
        public float EffectiveTrust { get; }
        public float Recency { get; }
        public float Priority { get; }
    }

    /// <summary>
    /// A bounded, allocation-free sound memory after construction. The caller supplies
    /// observation and evaluation times; no Unity clock or scene state is consulted.
    /// </summary>
    public sealed class HunterSoundMemory
    {
        public const int DefaultCapacity = 16;
        public const int MaximumCapacity = 64;
        public const double DefaultConfidenceHalfLife = 6d;
        public const double DefaultMaximumAge = 30d;
        public const float DefaultImpactRadius = 2f;
        public const double DefaultImpactWindow = 8d;
        public const double DefaultHabituationHalfLife = 20d;
        public const float DefaultPenaltyStep = 0.2f;
        public const float MaximumPenalty = 0.8f;
        private const float MinimumConfidence = 0.01f;

        private readonly HunterNoiseObservation[] observations;
        private readonly ImpactHabituation[] impactPlaces;
        private readonly double confidenceHalfLife;
        private readonly double maximumAge;
        private readonly float impactRadius;
        private readonly double impactWindow;
        private readonly double habituationHalfLife;
        private readonly float penaltyStep;
        private int count;
        private int impactPlaceCount;

        private struct ImpactHabituation
        {
            public float X;
            public float Y;
            public float Z;
            public double LastTime;
            public float Penalty;
        }

        public HunterSoundMemory(
            int capacity = DefaultCapacity,
            double confidenceHalfLife = DefaultConfidenceHalfLife,
            double maximumAge = DefaultMaximumAge,
            float impactRadius = DefaultImpactRadius,
            double impactWindow = DefaultImpactWindow,
            double habituationHalfLife = DefaultHabituationHalfLife,
            float penaltyStep = DefaultPenaltyStep)
        {
            Capacity = capacity < 1 ? 1 : Math.Min(capacity, MaximumCapacity);
            this.confidenceHalfLife = PositiveOrDefault(
                confidenceHalfLife, DefaultConfidenceHalfLife);
            this.maximumAge = PositiveOrDefault(maximumAge, DefaultMaximumAge);
            this.impactRadius = (float)PositiveOrDefault(
                impactRadius, DefaultImpactRadius);
            this.impactWindow = PositiveOrDefault(impactWindow, DefaultImpactWindow);
            this.habituationHalfLife = PositiveOrDefault(
                habituationHalfLife, DefaultHabituationHalfLife);
            this.penaltyStep = IsFinite(penaltyStep)
                ? Clamp01(penaltyStep)
                : DefaultPenaltyStep;
            observations = new HunterNoiseObservation[Capacity];
            impactPlaces = new ImpactHabituation[Capacity];
        }

        public int Capacity { get; }
        public int Count => count;

        /// <summary>
        /// Records a valid observation. Returns false for invalid data, a future
        /// timestamp, or a sound less relevant than every stored sound at capacity.
        /// </summary>
        public bool Record(HunterNoiseObservation observation, double now)
        {
            if (!IsValidTime(now) || !IsValid(observation) || observation.Time > now)
            {
                return false;
            }

            Prune(now);

            if (count == Capacity)
            {
                // Keep the most relevant observations when the array is full.
                // Equal scores let a new sound replace the older weakest entry.
                int weakest = 0;
                float weakestPriority = Evaluate(observations[0], now).Priority;

                for (int i = 1; i < count; i++)
                {
                    float priority = Evaluate(observations[i], now).Priority;
                    if (priority < weakestPriority ||
                        (priority == weakestPriority &&
                         observations[i].Time < observations[weakest].Time))
                    {
                        weakest = i;
                        weakestPriority = priority;
                    }
                }

                if (Evaluate(observation, now).Priority < weakestPriority)
                {
                    return false;
                }

                observations[weakest] = observation;
            }
            else
            {
                observations[count++] = observation;
            }

            if (observation.Category == NoiseType.Impact)
            {
                RecordImpact(observation);
            }

            return true;
        }

        /// <summary>
        /// Finds the highest-priority live observation. Ties prefer newer sounds,
        /// then the earlier stored entry. Invalid evaluation time returns false.
        /// </summary>
        public bool TryGetMostRelevant(double now, out HunterRememberedNoise result)
        {
            return TryGetMostRelevant(now, -1d, out result);
        }

        /// <summary>
        /// Finds the most relevant observation strictly newer than a resolved trail.
        /// This prevents an older, louder resolved sound from hiding a new quieter one.
        /// The original overload continues to evaluate all observations.
        /// </summary>
        public bool TryGetMostRelevant(double now, double afterTime, out HunterRememberedNoise result)
        {
            result = default;
            if (!IsValidTime(now) || double.IsNaN(afterTime) || double.IsInfinity(afterTime))
            {
                return false;
            }

            Prune(now);
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                HunterNoiseObservation observation = observations[i];
                if (observation.Time > now || observation.Time <= afterTime)
                {
                    continue;
                }

                HunterRememberedNoise candidate = Evaluate(observation, now);
                if (!found || candidate.Priority > result.Priority ||
                    (candidate.Priority == result.Priority &&
                     observation.Time > result.Observation.Time))
                {
                    result = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>Removes expired observations at the supplied time.</summary>
        public void Prune(double now)
        {
            if (!IsValidTime(now))
            {
                return;
            }

            for (int i = 0; i < count;)
            {
                double age = now - observations[i].Time;
                if (age >= 0d &&
                    (age > maximumAge || Confidence(observations[i], age) <
                     MinimumConfidence))
                {
                    for (int next = i + 1; next < count; next++)
                    {
                        observations[next - 1] = observations[next];
                    }

                    observations[--count] = default;
                }
                else
                {
                    i++;
                }
            }
        }

        private HunterRememberedNoise Evaluate(
            HunterNoiseObservation observation, double now)
        {
            double age = Math.Max(0d, now - observation.Time);
            float confidence = Confidence(observation, age);
            float recency = (float)(1d / (1d + age / confidenceHalfLife));
            float intensity = Clamp01(observation.Loudness /
                ReferenceLoudness(observation.Category));
            float trust = observation.Category == NoiseType.Impact
                ? ImpactTrust(observation, now)
                : 1f;
            float priority = Clamp01(
                intensity * confidence * recency * trust *
                CategoryWeight(observation.Category));

            return new HunterRememberedNoise(
                observation, intensity, confidence, trust, recency, priority);
        }

        private float ImpactTrust(HunterNoiseObservation observation, double now)
        {
            int index = FindImpactPlace(observation);
            if (index < 0)
            {
                return 1f;
            }

            ImpactHabituation place = impactPlaces[index];
            if (place.LastTime > now)
            {
                return 1f;
            }

            double age = Math.Max(0d, now - place.LastTime);
            return Clamp01(1f - place.Penalty * Decay(age, habituationHalfLife));
        }

        private void RecordImpact(HunterNoiseObservation observation)
        {
            int index = FindImpactPlace(observation);
            if (index < 0)
            {
                index = impactPlaceCount < Capacity
                    ? impactPlaceCount++
                    : OldestImpactPlace();
                impactPlaces[index] = new ImpactHabituation
                {
                    X = observation.X,
                    Y = observation.Y,
                    Z = observation.Z,
                    LastTime = observation.Time,
                    Penalty = 0f
                };
                return;
            }

            ImpactHabituation place = impactPlaces[index];
            double elapsed = observation.Time - place.LastTime;
            if (elapsed < 0d)
            {
                return; // A delayed observation cannot rewind learned trust.
            }

            float recoveredPenalty = place.Penalty *
                Decay(elapsed, habituationHalfLife);
            place.Penalty = elapsed <= impactWindow
                ? Math.Min(MaximumPenalty, recoveredPenalty + penaltyStep)
                : recoveredPenalty;
            place.LastTime = observation.Time;
            impactPlaces[index] = place;
        }

        private int FindImpactPlace(HunterNoiseObservation observation)
        {
            int best = -1;
            double nearestSquared = (double)impactRadius * impactRadius;

            for (int i = 0; i < impactPlaceCount; i++)
            {
                double dx = (double)observation.X - impactPlaces[i].X;
                double dy = (double)observation.Y - impactPlaces[i].Y;
                double dz = (double)observation.Z - impactPlaces[i].Z;
                double distanceSquared = dx * dx + dy * dy + dz * dz;
                if (distanceSquared <= nearestSquared)
                {
                    nearestSquared = distanceSquared;
                    best = i;
                }
            }

            return best;
        }

        private int OldestImpactPlace()
        {
            int oldest = 0;
            for (int i = 1; i < impactPlaceCount; i++)
            {
                if (impactPlaces[i].LastTime < impactPlaces[oldest].LastTime)
                {
                    oldest = i;
                }
            }

            return oldest;
        }

        private float Confidence(HunterNoiseObservation observation, double age)
        {
            return Clamp01(observation.InitialConfidence *
                Decay(age, confidenceHalfLife));
        }

        private static float Decay(double age, double halfLife)
        {
            return (float)Math.Pow(0.5d, age / halfLife);
        }

        private static float ReferenceLoudness(NoiseType category)
        {
            switch (category)
            {
                case NoiseType.Gunshot: return NoiseLevels.Gunshot;
                case NoiseType.Footstep: return NoiseLevels.RunningFootstep;
                case NoiseType.Impact: return NoiseLevels.Impact;
                case NoiseType.Door: return NoiseLevels.Door;
                case NoiseType.Alarm: return NoiseLevels.Alarm;
                default: return NoiseLevels.Alarm;
            }
        }

        private static float CategoryWeight(NoiseType category)
        {
            switch (category)
            {
                case NoiseType.Alarm: return 1f;
                case NoiseType.Gunshot: return 0.95f;
                case NoiseType.Door: return 0.7f;
                case NoiseType.Footstep: return 0.6f;
                case NoiseType.Impact: return 0.55f;
                default: return 0f;
            }
        }

        private static bool IsValid(HunterNoiseObservation observation)
        {
            return IsFinite(observation.X) && IsFinite(observation.Y) &&
                IsFinite(observation.Z) && IsValidTime(observation.Time) &&
                IsFinite(observation.Loudness) && observation.Loudness > 0f &&
                IsFinite(observation.InitialConfidence) &&
                observation.InitialConfidence > 0f &&
                observation.Category >= NoiseType.Gunshot &&
                observation.Category <= NoiseType.Alarm;
        }

        private static bool IsValidTime(double time)
        {
            return !double.IsNaN(time) && !double.IsInfinity(time) && time >= 0d;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static double PositiveOrDefault(double value, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value <= 0d
                ? fallback
                : value;
        }

        private static float Clamp01(float value)
        {
            return value <= 0f ? 0f : value >= 1f ? 1f : value;
        }
    }
}
