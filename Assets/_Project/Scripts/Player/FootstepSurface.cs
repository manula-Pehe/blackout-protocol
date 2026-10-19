using UnityEngine;

namespace BlackoutProtocol.Player
{
    public enum FootstepSurfaceType
    {
        Concrete,
        Metal
    }

    /// <summary>
    /// Identifies the material beneath the player for footstep sound and AI noise.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class FootstepSurface : MonoBehaviour
    {
        [SerializeField] private FootstepSurfaceType surfaceType;
        [SerializeField, Min(0f)] private float noiseMultiplier = 1f;
        [SerializeField] private AudioClip[] walkingClips;
        [SerializeField] private AudioClip[] runningClips;

        public string SurfaceName => surfaceType.ToString();
        public float NoiseMultiplier => noiseMultiplier;

        public AudioClip GetClip(bool running)
        {
            AudioClip[] clips = running ? runningClips : walkingClips;

            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            return clips[Random.Range(0, clips.Length)];
        }

        private void OnValidate()
        {
            noiseMultiplier = Mathf.Max(0f, noiseMultiplier);
        }
    }
}
