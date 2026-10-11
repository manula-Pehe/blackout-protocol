using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// A sound made somewhere in the level.
    /// </summary>
    public readonly struct NoiseEvent
    {
        /// <summary>
        /// Creates a noise event.
        /// </summary>
        /// <param name="position">World position where the sound was made.</param>
        /// <param name="loudness">Distance in metres at which the sound fades out in open space (see <see cref="NoiseLevels"/>).</param>
        /// <param name="type">Category of sound.</param>
        /// <param name="time">Game time when the sound was made (<c>Time.time</c>).</param>
        /// <param name="source">Object that made the sound (may be null).</param>
        public NoiseEvent(Vector3 position, float loudness, NoiseType type, float time, GameObject source = null)
        {
            Position = position;
            Loudness = Mathf.Max(0f, loudness);
            Type = type;
            Time = time;
            Source = source;
        }

        /// <summary>World position where the sound was made.</summary>
        public Vector3 Position { get; }

        /// <summary>Distance in metres at which the sound fades out in open space.</summary>
        public float Loudness { get; }

        /// <summary>Category of sound.</summary>
        public NoiseType Type { get; }

        /// <summary>Game time when the sound was made.</summary>
        public float Time { get; }

        /// <summary>Object that made the sound (may be null).</summary>
        public GameObject Source { get; }
    }

    /// <summary>
    /// Shared loudness values (metres of audible range in open space) so every
    /// noise source in the game uses the same scale.
    /// </summary>
    public static class NoiseLevels
    {
        public const float Gunshot = 30f;
        public const float RunningFootstep = 8f;
        public const float WalkingFootstep = 3f;
        public const float CrouchingFootstep = 0f;
        public const float Impact = 12f;
        public const float Door = 10f;
        public const float Alarm = 50f;
    }
}
