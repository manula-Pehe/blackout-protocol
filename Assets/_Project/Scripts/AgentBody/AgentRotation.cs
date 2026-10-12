using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>
    /// Turns the agent around the Y axis at a limited rate. Uses
    /// <see cref="Quaternion.RotateTowards"/> so the turn speed is a constant number of
    /// degrees per second, independent of frame rate and of how far there is to turn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AgentRotation : MonoBehaviour
    {
        [Tooltip("Maximum turn speed in degrees per second.")]
        [SerializeField] private float turnRate = 360f;

        /// <summary>Maximum turn speed in degrees per second.</summary>
        public float TurnRate
        {
            get => turnRate;
            set => turnRate = Mathf.Max(0f, value);
        }

        /// <summary>Turns toward a world-space direction (height is ignored).</summary>
        public void FaceDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                target,
                turnRate * deltaTime);
        }

        /// <summary>Turns toward a world-space point.</summary>
        public void FacePoint(Vector3 point, float deltaTime)
        {
            FaceDirection(point - transform.position, deltaTime);
        }

        /// <summary>Horizontal angle in degrees between the agent's forward and a direction.</summary>
        public float AngleTo(Vector3 direction)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            direction.y = 0f;

            if (forward.sqrMagnitude < 0.0001f || direction.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            return Vector3.Angle(forward, direction);
        }
    }
}
