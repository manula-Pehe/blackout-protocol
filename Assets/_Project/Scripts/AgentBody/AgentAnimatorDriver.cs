using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>
    /// Feeds actual ground movement into one configurable float parameter. Animation
    /// remains in place: this component never moves the body or chooses an AI action.
    /// </summary>
    [RequireComponent(typeof(AgentBody))]
    [DisallowMultipleComponent]
    public sealed class AgentAnimatorDriver : MonoBehaviour
    {
        public enum BindingStatus { MissingAnimator, MissingController, MissingFloatParameter, InactiveAnimator, NotInitialized, Ready }

        [Tooltip("Animator on the robot model. Found in children if left empty.")]
        [SerializeField] private Animator animator;

        [Tooltip("Float parameter in the model's Animator Controller.")]
        [SerializeField] private string speedParameter = "Speed";

        [Tooltip("Divide actual m/s by this value. 1 preserves raw m/s; use the clip's run speed for normalized Run = 1.")]
        [SerializeField, Min(0.01f)] private float referenceSpeed = 1f;

        [Tooltip("Actual horizontal m/s at or below this value is treated as Idle.")]
        [SerializeField, Min(0)] private float idleSpeedThreshold = 0.02f;

        [Tooltip("Exponential response time in seconds. Zero writes the target without damping.")]
        [SerializeField, Min(0)] private float speedDampTime = 0.1f;

        private AgentBody body;
        private Animator cachedAnimator;
        private RuntimeAnimatorController cachedController;
        private string cachedParameter;
        private int speedId;
        private bool parameterFound, bindingChecked;

        public float ActualHorizontalSpeed { get; private set; }
        public float TargetSpeedParameter { get; private set; }
        public float SmoothedSpeedParameter { get; private set; }
        public BindingStatus Status { get; private set; }
        public bool IsAnimatorReady => isActiveAndEnabled && Status == BindingStatus.Ready;
        public int BindingValidationCount { get; private set; }

        private void Awake()
        {
            RefreshAnimatorBinding();
        }

        private void OnEnable() => RefreshAnimatorBinding();

        /// <summary>Call after attaching/replacing a model at runtime. Also available in the component menu.</summary>
        [ContextMenu("Refresh Animator Binding")]
        public void RefreshAnimatorBinding()
        {
            if (body == null) body = GetComponent<AgentBody>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            bindingChecked = false;
            if (animator != null) animator.applyRootMotion = false;
            ValidateBinding();
        }

        // AgentBody moves in Update. Sample afterwards, not an arbitrary previous-frame velocity.
        private void LateUpdate()
        {
            ActualHorizontalSpeed = body != null ? LocomotionAnimation.HorizontalSpeed(body.Velocity) : 0;
            TargetSpeedParameter = LocomotionAnimation.TargetParameter(ActualHorizontalSpeed, referenceSpeed, idleSpeedThreshold);
            SmoothedSpeedParameter = LocomotionAnimation.Smooth(SmoothedSpeedParameter, TargetSpeedParameter, speedDampTime, Time.deltaTime);
            ValidateBinding();
            if (IsAnimatorReady) animator.SetFloat(speedId, SmoothedSpeedParameter);
        }

        private void ValidateBinding()
        {
            if (animator == null) { Status = BindingStatus.MissingAnimator; return; }
            // Enforce this on a new binding and if a caller explicitly re-enables root motion.
            if (animator.applyRootMotion) animator.applyRootMotion = false;
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller != null && !animator.isActiveAndEnabled) { Status = BindingStatus.InactiveAnimator; return; }
            if (controller != null && !animator.isInitialized) { Status = BindingStatus.NotInitialized; return; }
            if (!bindingChecked || cachedAnimator != animator || cachedController != controller || cachedParameter != speedParameter)
            {
                bindingChecked = true;
                cachedAnimator = animator;
                cachedController = controller;
                cachedParameter = speedParameter;
                parameterFound = false;
                BindingValidationCount++;
                if (controller != null && !string.IsNullOrEmpty(speedParameter))
                {
                    speedId = Animator.StringToHash(speedParameter);
                    // Unity returns a new array: inspect only when binding/controller/name changes.
                    foreach (AnimatorControllerParameter parameter in animator.parameters)
                        if (parameter.nameHash == speedId && parameter.type == AnimatorControllerParameterType.Float)
                        { parameterFound = true; break; }
                }
            }
            if (controller == null) Status = BindingStatus.MissingController;
            else if (!parameterFound) Status = BindingStatus.MissingFloatParameter;
            else if (!animator.isActiveAndEnabled) Status = BindingStatus.InactiveAnimator;
            else if (!animator.isInitialized) Status = BindingStatus.NotInitialized;
            else Status = BindingStatus.Ready;
        }

        private void OnDisable()
        {
            ActualHorizontalSpeed = TargetSpeedParameter = SmoothedSpeedParameter = 0;
            if (animator != null && animator.isActiveAndEnabled && animator.isInitialized &&
                cachedAnimator == animator && cachedParameter == speedParameter && bindingChecked &&
                animator.runtimeAnimatorController == cachedController && parameterFound)
                animator.SetFloat(speedId, 0);
        }

        private void OnValidate()
        {
            referenceSpeed = float.IsNaN(referenceSpeed) || float.IsInfinity(referenceSpeed) ? 1 : Mathf.Max(0.01f, referenceSpeed);
            idleSpeedThreshold = float.IsNaN(idleSpeedThreshold) || float.IsInfinity(idleSpeedThreshold) ? 0.02f : Mathf.Max(0, idleSpeedThreshold);
            speedDampTime = float.IsNaN(speedDampTime) || float.IsInfinity(speedDampTime) ? 0.1f : Mathf.Max(0, speedDampTime);
            bindingChecked = false;
        }
    }
}
