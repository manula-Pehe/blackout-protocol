using System.Collections.Generic;
using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// Marks a position in the level where an agent can take cover.
    /// The transform's forward direction points away from the protecting obstacle,
    /// towards where threats are expected to come from.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoverPoint : MonoBehaviour
    {
        private static readonly List<CoverPoint> ActivePoints = new List<CoverPoint>();

        [SerializeField, Tooltip("Height of the protecting obstacle in metres. Low cover (< 1.2 m) protects a crouching agent only.")]
        private float coverHeight = 1.5f;

        /// <summary>All enabled cover points in the loaded scenes.</summary>
        public static IReadOnlyList<CoverPoint> All => ActivePoints;

        /// <summary>World position of the cover point.</summary>
        public Vector3 Position => transform.position;

        /// <summary>Direction the cover faces (away from the obstacle).</summary>
        public Vector3 Facing => transform.forward;

        /// <summary>Height of the protecting obstacle in metres.</summary>
        public float CoverHeight => coverHeight;

        /// <summary>The agent currently using this cover point, or null.</summary>
        public GameObject Occupant { get; private set; }

        /// <summary>True when no agent is using this cover point.</summary>
        public bool IsFree => Occupant == null;

        /// <summary>Claims the cover point for an agent. Returns false if another agent already holds it.</summary>
        public bool TryClaim(GameObject agent)
        {
            if (Occupant != null && Occupant != agent)
            {
                return false;
            }

            Occupant = agent;
            return true;
        }

        /// <summary>Releases the cover point if the given agent holds it.</summary>
        public void Release(GameObject agent)
        {
            if (Occupant == agent)
            {
                Occupant = null;
            }
        }

        private void OnEnable()
        {
            ActivePoints.Add(this);
        }

        private void OnDisable()
        {
            ActivePoints.Remove(this);
            Occupant = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsFree ? new Color(0.2f, 0.9f, 1f) : new Color(1f, 0.6f, 0.1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + transform.forward * 0.8f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * coverHeight);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            ActivePoints.Clear();
        }
    }
}
