using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>
    /// Feeds the body's real ground speed into the Animator. Animations are in place
    /// (root motion off): the path follower owns the position so AI paths stay exact,
    /// and the animation only shows the walk or run cycle.
    /// </summary>
    [RequireComponent(typeof(AgentBody))]
    [DisallowMultipleComponent]
    public sealed class AgentAnimatorDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");

        [Tooltip("Animator on the robot model. Found in children if left empty.")]
        [SerializeField] private Animator animator;

        [Tooltip("Smoothing time for the Speed parameter, in seconds.")]
        [SerializeField] private float speedDampTime = 0.1f;

        private AgentBody body;

        private void Awake()
        {
            body = GetComponent<AgentBody>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(SpeedId, body.Velocity.magnitude, speedDampTime, Time.deltaTime);
        }
    }
}
