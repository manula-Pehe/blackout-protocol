using System;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Body
{
    // Owns the humanoid visual; the brain still owns all damage and targeting decisions.
    [RequireComponent(typeof(Animator)), DisallowMultipleComponent]
    public sealed class AgentActionAnimation : MonoBehaviour
    {
        private static readonly int X = Animator.StringToHash("LocalX");
        private static readonly int Z = Animator.StringToHash("LocalZ");
        private static readonly int Rate = Animator.StringToHash("StrideRate");
        private static readonly int Sneak = Animator.StringToHash("Sneak");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int AttackRate = Animator.StringToHash("AttackRate");
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int Die = Animator.StringToHash("Die");
        private static readonly int Alert = Animator.StringToHash("Alert");
        private static readonly int AttackFullPath = Animator.StringToHash("Upper Body.Attack");
        private static readonly int DeathState = Animator.StringToHash("Death");
        [SerializeField, Min(0.1f)] private float walkReference = 1.6f;
        [SerializeField, Min(0.1f)] private float runReference = 4f;
        [SerializeField, Min(0.1f)] private float sneakReference = 1f;
        [SerializeField, Min(0)] private float damping = 0.09f;
        [Header("Rigid robot hand stabilization (locomotion only)")]
        [SerializeField, Range(0,1)] private float fingerRestWeight = 1f;
        [SerializeField, Range(0,1)] private float wristRestWeight = 0.65f;
        private readonly Transform[] handBones = new Transform[32];
        private readonly Quaternion[] handRest = new Quaternion[32];
        private int handBoneCount, wristBoneCount;
        private Renderer[] modelRenderers;
        private AgentBody body;
        private Animator animator;
        private bool dead, ready, hasAttackRate, impactDelivered = true;
        private int upperLayer = -1;
        private float attackClipLength, impactFraction = 0.45f;
        private float attackEnd = -1f, impactDeadline = -1f, impactEarliest = -1f;
        private float scheduledCycle, lastStep = -10f;
        private Action pendingImpact;
        public event Action AttackImpact;
        public event Action Footstep;
        public bool IsReady => ready && isActiveAndEnabled && animator != null && animator.isActiveAndEnabled;
        public bool SupportsTimedAttack => IsReady && !dead && hasAttackRate && attackClipLength > 0 && body != null;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            body = GetComponentInParent<AgentBody>();
            modelRenderers = GetComponentsInChildren<Renderer>(true);
            // Capture the model-authored local wrist/finger rotations before animation evaluation.
            // No shoulder/elbow override: those remain driven by the retargeted humanoid clips.
            if (animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
            {
                CacheHand(HumanBodyBones.LeftHand);
                CacheHand(HumanBodyBones.RightHand);
                wristBoneCount = handBoneCount;
                for (int i = (int)HumanBodyBones.LeftThumbProximal; i <= (int)HumanBodyBones.RightLittleDistal; i++)
                    CacheHand((HumanBodyBones)i);
            }
        }
        private void CacheHand(HumanBodyBones bone)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null || handBoneCount >= handBones.Length) return;
            handBones[handBoneCount] = t;
            handRest[handBoneCount++] = t.localRotation;
        }
        private void OnEnable()
        {
            ready = animator != null && animator.runtimeAnimatorController != null &&
                animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
            hasAttackRate = false; attackClipLength = 0;
            if (ready)
            {
                bool x = false, z = false, rate = false, sneak = false, attack = false, hit = false, die = false, alert = false;
                foreach (var p in animator.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Float)
                    { x |= p.nameHash == X; z |= p.nameHash == Z; rate |= p.nameHash == Rate; hasAttackRate |= p.nameHash == AttackRate; }
                    if (p.type == AnimatorControllerParameterType.Bool) sneak |= p.nameHash == Sneak;
                    if (p.type == AnimatorControllerParameterType.Trigger) { attack |= p.nameHash == Attack; hit |= p.nameHash == Hit; die |= p.nameHash == Die; alert |= p.nameHash == Alert; }
                }
                upperLayer = animator.GetLayerIndex("Upper Body");
                ready = x && z && rate && sneak && attack && hit && die && alert && upperLayer >= 0 &&
                    animator.HasState(upperLayer, AttackFullPath);
                // Read clip/event metadata once, never allocate clip arrays in the frame loop.
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip != null && clip.name == "Attack" && clip.length > 0)
                    {
                        foreach (var e in clip.events)
                            if (e.functionName == nameof(AnimationAttackImpact))
                            { attackClipLength = clip.length; impactFraction = Mathf.Clamp01(e.time / clip.length); break; }
                        break;
                    }
            }
            if (body != null) body.ActionRequested += OnAction;
            if (ready)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.SetLayerWeight(upperLayer, 0f);
            }
        }
        private void OnDisable()
        {
            if (body != null) body.ActionRequested -= OnAction;
            CancelPendingAttack(); impactDelivered = true; attackEnd = -1f; scheduledCycle = 0;
            if (ready && animator != null && upperLayer >= 0 && upperLayer < animator.layerCount)
                animator.SetLayerWeight(upperLayer, 0f);
            ready = false;
        }
        public bool TryPlayTimedAttack(float cycleSeconds, Action onImpact)
        {
            if (!SupportsTimedAttack || onImpact == null || !impactDelivered || Time.time < attackEnd) return false;
            pendingImpact = onImpact;
            scheduledCycle = Mathf.Max(0.35f, cycleSeconds);
            body.PlayAction(AgentAction.Attack);
            return true;
        }
        public void CancelPendingAttack() { pendingImpact = null; }
        private void DispatchImpact()
        {
            if (impactDelivered || dead || !IsReady) return;
            impactDelivered = true;
            Action callback = pendingImpact; pendingImpact = null;
            callback?.Invoke();
            AttackImpact?.Invoke();
        }
        private static bool UpperAction(AnimatorStateInfo state) => state.shortNameHash == Attack || state.shortNameHash == Alert;
        private void UpdateUpperBodyWeight()
        {
            bool active = UpperAction(animator.GetCurrentAnimatorStateInfo(upperLayer));
            if (animator.IsInTransition(upperLayer))
                active = UpperAction(animator.GetNextAnimatorStateInfo(upperLayer));
            float weight = Mathf.MoveTowards(animator.GetLayerWeight(upperLayer), active ? 1f : 0f, Time.deltaTime / 0.09f);
            animator.SetLayerWeight(upperLayer, weight);
        }
        private void StabilizeHands()
        {
            // Keep off-screen transform work culled as well as the Animator itself.
            bool visible = false;
            for (int i = 0; i < modelRenderers.Length; i++)
                if (modelRenderers[i] != null && modelRenderers[i].isVisible) { visible = true; break; }
            if (!visible) return;
            float locomotionWeight = 1f - animator.GetLayerWeight(upperLayer);
            // The first two cached transforms are wrists (if present); distinguish by reference.
            for (int i = 0; i < handBoneCount; i++)
            {
                Transform t = handBones[i]; if (t == null) continue;
                float weight = (i < wristBoneCount ? wristRestWeight : fingerRestWeight) * locomotionWeight;
                if (weight > 0f) t.localRotation = Quaternion.Slerp(t.localRotation, handRest[i], weight);
            }
        }
        private void LateUpdate()
        {
            if (!IsReady || body == null) return;
            // A safety net for culled/missing events: exactly one impact, even off-screen.
            if (!impactDelivered && !dead && Time.time >= impactDeadline) DispatchImpact();
            if (!IsReady || !animator.isInitialized) return;
            if (dead)
            {
                if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == DeathState &&
                    animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                return;
            }
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
            // Blend walk/run reference speeds continuously, rather than abruptly halving cadence.
            float blend = Mathf.InverseLerp(walkReference, runReference, speed);
            float nominal = sneaking ? sneakReference : Mathf.Lerp(walkReference, runReference, blend);
            animator.SetFloat(Rate, speed <= 0.02f ? 1f : Mathf.Clamp(speed / Mathf.Max(0.1f, nominal), 0.35f, 2f), damping, Time.deltaTime);
            StabilizeHands();
        }
        private void OnAction(AgentAction action)
        {
            if (!IsReady || dead) return;
            if (action == AgentAction.Die)
            {
                dead = true; CancelPendingAttack(); impactDelivered = true;
                animator.ResetTrigger(Attack); animator.ResetTrigger(Hit); animator.ResetTrigger(Alert);
                animator.SetTrigger(Die); animator.SetLayerWeight(upperLayer, 0f);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (body != null)
                {
                    body.Stop(); body.enabled = false;
                    var collision = body.GetComponent<CharacterController>(); if (collision != null) collision.enabled = false;
                }
            }
            else if (action == AgentAction.Attack)
            {
                if (!impactDelivered || Time.time < attackEnd) return;
                float clipLength = attackClipLength > 0 ? attackClipLength : 1.1f;
                float cycle = scheduledCycle > 0 ? scheduledCycle : clipLength;
                scheduledCycle = 0;
                if (hasAttackRate) animator.SetFloat(AttackRate, clipLength / cycle);
                else cycle = clipLength;
                attackEnd = Time.time + cycle;
                impactEarliest = Time.time + cycle * impactFraction * 0.5f;
                impactDeadline = Time.time + Mathf.Min(cycle, cycle * impactFraction + 0.12f);
                impactDelivered = false;
                animator.ResetTrigger(Alert); animator.ResetTrigger(Attack);
                // A fixed-duration crossfade reliably restarts a new swing even if the old exit blend is finishing.
                // A small nonzero weight ensures a previously idle override layer evaluates.
                if (animator.GetLayerWeight(upperLayer) < 0.01f) animator.SetLayerWeight(upperLayer, 0.01f);
                if (animator.isInitialized) animator.CrossFadeInFixedTime(AttackFullPath, 0.06f, upperLayer, 0f);
                else animator.SetTrigger(Attack);
            }
            else if (action == AgentAction.Hit) animator.SetTrigger(Hit);
            else if (action == AgentAction.Alert && Time.time >= attackEnd)
            {
                if (animator.GetLayerWeight(upperLayer) < 0.01f) animator.SetLayerWeight(upperLayer, 0.01f);
                animator.SetTrigger(Alert);
            }
        }
        public void AnimationFootstep()
        {
            if (!IsReady || dead || body == null || !body.IsMoving || body.Velocity.sqrMagnitude < 0.01f) return;
            float minimumInterval = body.Style == MoveStyle.Run ? 0.20f : 0.34f;
            if (Time.time - lastStep < minimumInterval) return;
            lastStep = Time.time;
            float loudness = body.Style == MoveStyle.Run ? NoiseLevels.RunningFootstep : body.Style == MoveStyle.Sneak ? NoiseLevels.CrouchingFootstep : NoiseLevels.WalkingFootstep;
            if (loudness > 0) GameEvents.RaiseNoise(new NoiseEvent(body.Position, loudness, NoiseType.Footstep, Time.time, body.gameObject));
            Footstep?.Invoke();
        }
        public void AnimationAttackImpact()
        {
            if (IsReady && !dead && Time.time >= impactEarliest) DispatchImpact();
        }
    }
}
