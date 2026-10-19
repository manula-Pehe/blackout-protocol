namespace BlackoutProtocol.AI.Hunter.Logic
{
    /// <summary>
    /// Normalized facts used for one Hunter decision. Every value is sanitized to
    /// the inclusive range 0..1, so noisy runtime inputs cannot produce invalid scores.
    /// </summary>
    public readonly struct HunterUtilityContext
    {
        public HunterUtilityContext(
            float soundIntensity,
            float memoryConfidence,
            float noiseTrust,
            float targetProximity,
            float searchUrgency,
            float attackEligibility)
        {
            SoundIntensity = Normalize(soundIntensity);
            MemoryConfidence = Normalize(memoryConfidence);
            NoiseTrust = Normalize(noiseTrust);
            TargetProximity = Normalize(targetProximity);
            SearchUrgency = Normalize(searchUrgency);
            AttackEligibility = Normalize(attackEligibility);
        }

        /// <summary>Perceived loudness after range and future propagation effects.</summary>
        public float SoundIntensity { get; }

        /// <summary>Confidence that the Hunter still knows where its target is.</summary>
        public float MemoryConfidence { get; }

        /// <summary>Learned reliability of the current noise source or category.</summary>
        public float NoiseTrust { get; }

        /// <summary>Closeness to the remembered target: 0 is far, 1 is touching.</summary>
        public float TargetProximity { get; }

        /// <summary>Pressure to search after losing a previously useful trail.</summary>
        public float SearchUrgency { get; }

        /// <summary>Readiness to attack; zero is a hard gate and one is fully eligible.</summary>
        public float AttackEligibility { get; }

        private static float Normalize(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
            {
                return 0f;
            }

            if (float.IsPositiveInfinity(value) || value >= 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}
