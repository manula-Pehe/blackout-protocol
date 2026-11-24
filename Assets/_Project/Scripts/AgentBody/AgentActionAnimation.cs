using System;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Body
{
    // Lives on the Animator object so Unity animation events reach this receiver.
    [RequireComponent(typeof(Animator)), DisallowMultipleComponent]
    public sealed class AgentActionAnimation : MonoBehaviour
    {
        private static readonly int X = Animator.StringToHash("LocalX");
        private static readonly int Z = Animator.StringToHash("LocalZ");
        private static readonly int Rate = Animator.StringToHash("StrideRate");
        private static readonly int Sneak = Animator.StringToHash("Sneak");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int Die = Animator.StringToHash("Die");
        private static readonly int Alert = Animator.StringToHash("Alert");
        [SerializeField, Min(0.1f)] private float walkReference = 1.6f;
        [SerializeField, Min(0.1f)] private float runReference = 4f;
        [SerializeField, Min(0.1f)] private float sneakReference = 1f;
        [SerializeField, Min(0)] private float damping = 0.09f;
        private AgentBody body;
        private Animator animator;
        private bool dead, ready;
        private int upperLayer = -1;
        private float actionRequestUntil = -1f;
        private static readonly int AttackState = Animator.StringToHash("Attack");
        private static readonly int AlertState = Animator.StringToHash("Alert");
        private float lastStep = -10f;
        public event Action AttackImpact;
        public event Action Footstep;
        public bool IsReady => ready && isActiveAndEnabled && animator != null && animator.isActiveAndEnabled;
        private void Awake() { animator = GetComponent<Animator>(); body = GetComponentInParent<AgentBody>(); }
        private void OnEnable()
        {
            ready = animator != null && animator.runtimeAnimatorController != null;
            if (ready)
            {
                bool x = false, z = false, rate = false, sneak = false, attack = false, hit = false, die = false, alert = false;
                foreach (var p in animator.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Float) { x |= p.nameHash == X; z |= p.nameHash == Z; rate |= p.nameHash == Rate; }
                    if (p.type == AnimatorControllerParameterType.Bool) sneak |= p.nameHash == Sneak;
                    if (p.type == AnimatorControllerParameterType.Trigger) { attack |= p.nameHash == Attack; hit |= p.nameHash == Hit; die |= p.nameHash == Die; alert |= p.nameHash == Alert; }
                }
                ready = x && z && rate && sneak && attack && hit && die && alert;
            }
            if (body != null) body.ActionRequested += OnAction;
            if (ready)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                upperLayer = animator.GetLayerIndex("Upper Body");
                actionRequestUntil = -1f;
                if (upperLayer >= 0) animator.SetLayerWeight(upperLayer, 0f);
            }
        }
        private void OnDisable()
        {
            if (body != null) body.ActionRequested -= OnAction;
            if (ready && animator != null && upperLayer >= 0 && upperLayer < animator.layerCount)
                animator.SetLayerWeight(upperLayer, 0f);
            ready = false;
        }
        private bool IsUpperAction(AnimatorStateInfo state) =>
            state.shortNameHash == AttackState || state.shortNameHash == AlertState;

        private void UpdateUpperBodyWeight()
        {
            if (upperLayer < 0 || upperLayer >= animator.layerCount) return;
            bool active = IsUpperAction(animator.GetCurrentAnimatorStateInfo(upperLayer));
            if (animator.IsInTransition(upperLayer))
                active |= IsUpperAction(animator.GetNextAnimatorStateInfo(upperLayer));
            if (active) actionRequestUntil = -1f;
            animator.SetLayerWeight(upperLayer, active || Time.time < actionRequestUntil ? 1f : 0f);
        }

        private void RequestUpperAction(int trigger)
        {
            if (upperLayer >= 0 && upperLayer < animator.layerCount)
            {
                // Give the Animator time to enter the requested state before inspecting it.
                actionRequestUntil = Time.time + 0.5f;
                animator.SetLayerWeight(upperLayer, 1f);
            }
            animator.SetTrigger(trigger);
        }
        private void LateUpdate()
        {
            if (!IsReady || body == null || dead || !animator.isInitialized) return;
            UpdateUpperBodyWeight();
            Vector3 local = body.transform.InverseTransformDirection(body.Velocity);
            local.y = 0;
            float speed = local.magnitude;
            bool sneaking = body.Style == MoveStyle.Sneak && speed > 0.02f;
            Vector2 axis = speed > 0.02f ? new Vector2(local.x, local.z) / Mathf.Max(0.1f, runReference) : Vector2.zero;
            axis = Vector2.ClampMagnitude(axis, 1f);
            animator.SetFloat(X, axis.x, damping, Time.deltaTime);
            animator.SetFloat(Z, axis.y, damping, Time.deltaTime);
            animator.SetBool(Sneak, sneaking);
            // Approximate stride matching against the nearest authored gait; tune on the actual rig.
            float nominal = sneaking ? sneakReference : speed > (walkReference + runReference) * 0.5f ? runReference : walkReference;
            animator.SetFloat(Rate, speed <= 0.02f ? 1f : Mathf.Clamp(speed / Mathf.Max(0.1f, nominal), 0.35f, 2f), damping, Time.deltaTime);
        }
        private void OnAction(AgentAction action)
        {
            if (!IsReady || dead) return;
            if (action == AgentAction.Die)
            {
                dead = true;
                animator.ResetTrigger(Attack); animator.ResetTrigger(Hit); animator.ResetTrigger(Alert);
                animator.SetTrigger(Die);
                int layer = animator.GetLayerIndex("Upper Body");
                if (layer >= 0) animator.SetLayerWeight(layer, 0f);
                // Death is full-body, and must finish even when the model is offscreen.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (body != null) { body.Stop(); var collision = body.GetComponent<CharacterController>(); if (collision != null) collision.enabled = false; }
            }
            else if (action == AgentAction.Attack) RequestUpperAction(Attack);
            else if (action == AgentAction.Hit) animator.SetTrigger(Hit);
            else if (action == AgentAction.Alert) RequestUpperAction(Alert);
        }
        // Events are authored on copies of clips, not on the source FBXs.
        public void AnimationFootstep()
        {
            if (!IsReady || dead || body == null || !body.IsMoving || body.Velocity.sqrMagnitude < 0.01f || Time.time - lastStep < 0.15f) return;
            lastStep = Time.time;
            float loudness = body.Style == MoveStyle.Run ? NoiseLevels.RunningFootstep : body.Style == MoveStyle.Sneak ? NoiseLevels.CrouchingFootstep : NoiseLevels.WalkingFootstep;
            if (loudness > 0) GameEvents.RaiseNoise(new NoiseEvent(body.Position, loudness, NoiseType.Footstep, Time.time, body.gameObject));
            Footstep?.Invoke();
        }
        // Visual/event evidence only in 03A. Existing brains remain the single damage owner.
        public void AnimationAttackImpact() { if (IsReady && !dead) AttackImpact?.Invoke(); }
    }
}
