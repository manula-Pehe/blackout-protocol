using BlackoutProtocol.Core;

namespace BlackoutProtocol.AI.Hunter.Logic
{
    /// <summary>
    /// A sound heard at a world position. Loudness uses the shared NoiseEvent meaning:
    /// audible range in metres in open space. Confidence is the caller's initial
    /// perception confidence after any distance or obstruction effects.
    /// </summary>
    public readonly struct HunterNoiseObservation
    {
        public HunterNoiseObservation(
            float x,
            float y,
            float z,
            NoiseType category,
            double time,
            float loudness,
            float initialConfidence)
        {
            X = x;
            Y = y;
            Z = z;
            Category = category;
            Time = time;
            Loudness = loudness;
            InitialConfidence = initialConfidence;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public NoiseType Category { get; }
        public double Time { get; }
        public float Loudness { get; }
        public float InitialConfidence { get; }
    }
}
