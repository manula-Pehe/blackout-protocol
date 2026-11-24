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
            SourceX = x;
            SourceY = y;
            SourceZ = z;
            OriginalLoudness = loudness;
            PerceivedLoudness = loudness;
            AcousticDistance = 0f;
            ClosedDoors = 0;
            IncomingX = x;
            IncomingY = y;
            IncomingZ = z;
            UncertaintyRadius = 0f;
        }

        public HunterNoiseObservation(float estimatedX, float estimatedY, float estimatedZ,
            float sourceX, float sourceY, float sourceZ, NoiseType category, double time,
            float perceivedLoudness, float originalLoudness, float initialConfidence,
            float acousticDistance, int closedDoors, float incomingX, float incomingY,
            float incomingZ, float uncertaintyRadius)
        {
            X = estimatedX;
            Y = estimatedY;
            Z = estimatedZ;
            SourceX = sourceX;
            SourceY = sourceY;
            SourceZ = sourceZ;
            Category = category;
            Time = time;
            // Base category loudness preserves the existing Utility AI contract;
            // acoustic loss is applied once through InitialConfidence.
            Loudness = originalLoudness;
            OriginalLoudness = originalLoudness;
            PerceivedLoudness = perceivedLoudness;
            InitialConfidence = initialConfidence;
            AcousticDistance = acousticDistance;
            ClosedDoors = closedDoors < 0 ? 0 : closedDoors;
            IncomingX = incomingX;
            IncomingY = incomingY;
            IncomingZ = incomingZ;
            UncertaintyRadius = uncertaintyRadius < 0f ? 0f : uncertaintyRadius;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public NoiseType Category { get; }
        public double Time { get; }
        public float Loudness { get; }
        public float InitialConfidence { get; }
        public float SourceX { get; }
        public float SourceY { get; }
        public float SourceZ { get; }
        public float OriginalLoudness { get; }
        public float PerceivedLoudness { get; }
        public float AcousticDistance { get; }
        public int ClosedDoors { get; }
        public float IncomingX { get; }
        public float IncomingY { get; }
        public float IncomingZ { get; }
        public float UncertaintyRadius { get; }
    }
}
