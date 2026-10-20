using System.Collections.Generic;
using BlackoutProtocol.AI.Hunter.Logic;
using BlackoutProtocol.AI.Hunter.Testing;
using BlackoutProtocol.Body;
using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.AI.Hunter
{
    /// <summary>Runtime coordinator. Utility scores, memory, route search and movement remain in their existing components.</summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AgentBody), typeof(HunterNavMeshPathfinder))]
    public sealed class HunterBrain : MonoBehaviour
    {
        [SerializeField] private AgentBody body;
        [SerializeField] private HunterNavMeshPathfinder pathfinder;
        [SerializeField] private Transform[] wanderPoints = new Transform[0];
        [SerializeField] private LineRenderer pathDisplay;
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.2f;
        [SerializeField, Min(0.1f)] private float minimumRepathInterval = 0.75f;
        [SerializeField, Min(0.1f)] private float movingTargetRepathDistance = 0.8f;
        [SerializeField, Range(0, 1)] private float hysteresisBonus = 0.08f;
        [Header("Evidence lifetime (seconds)")]
        [SerializeField, Min(0.1f)] private float memoryHalfLife = 24f;
        [SerializeField, Min(1)] private float memoryMaximumAge = 60f;
        [SerializeField, Range(0.05f, 1)] private float minimumTrailConfidence = 0.35f;
        [SerializeField, Min(0.1f)] private float searchDuration = 8f;
        [SerializeField, Min(0.1f)] private float searchRadius = 1.5f;
        [Header("Visual target and attack")]
        [SerializeField, Min(1)] private float sightRange = 12f;
        [SerializeField, Min(1)] private float noiseProximityRange = 24f;
        [SerializeField] private LayerMask sightMask = ~0;
        [SerializeField, Min(0.1f)] private float attackRange = 1.4f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.2f;
        [SerializeField, Min(0)] private float attackDamage = 10f;

        private readonly List<Vector3> path = new List<Vector3>(64);
        private HunterSoundMemory memory;
        private HunterUtilitySelector selector;
        private IAgentBody agentBody;
        private IPathfinder routeFinder;
        private IPlayerState visibleTarget;
        private Vector3 home, trailPosition, routeGoal;
        private bool subscribed, routeActive, hasTrail, searching, forceRepath;
        private float nextDecision, nextPath, nextMove, searchEnds, nextAttack;
        private double resolvedThroughTime = -1;
        private int wanderIndex, searchIndex, stuckRetries;
        private readonly HunterTransitionHistory transitions = new HunterTransitionHistory();
        private HunterDecisionReason pendingReason;
        private Vector3 blockedPursuitGoal;
        private float blockedPursuitUntil, nextStuckRecovery;
        private bool previouslyVisible;
        private double lastFreshNoiseTime = -1;

        public HunterAction CurrentAction { get; private set; } = HunterAction.Wander;
        public HunterUtilitySelection LastSelection { get; private set; }
        public HunterUtilityContext LastContext { get; private set; }
        public HunterRememberedNoise SelectedNoise { get; private set; }
        public bool HasSelectedNoise { get; private set; }
        public bool HasVisibleTarget => ValidTarget(visibleTarget);
        public string Status { get; private set; } = "Waiting for the first decision.";
        public int AcceptedNoiseCount { get; private set; }
        public int PathRequestCount { get; private set; }
        public int PathFailureCount { get; private set; }
        public int ArrivalCount { get; private set; }
        public int StuckRecoveryCount { get; private set; }
        public int AttackCount { get; private set; }
        public IReadOnlyList<Vector3> Waypoints => path;
        public HunterTransitionHistory Transitions => transitions;
        public double LastDecisionTime { get; private set; }
        public int DecisionCount { get; private set; }
        public bool HasActiveRoute => routeActive && agentBody != null && agentBody.IsMoving;
        public bool HasDestination { get; private set; }
        public Vector3 Destination => routeGoal;
        public string LastRuntimeEvent { get; private set; } = "No runtime events yet.";

        private void Awake()
        {
            if (body == null) body = GetComponent<AgentBody>();
            if (pathfinder == null) pathfinder = GetComponent<HunterNavMeshPathfinder>();
            agentBody = body;
            routeFinder = pathfinder;
            memory = new HunterSoundMemory(confidenceHalfLife: memoryHalfLife, maximumAge: memoryMaximumAge);
            selector = new HunterUtilitySelector(hysteresisBonus);
            home = transform.position;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || agentBody == null || subscribed) return;
            if (TryGetComponent(out HunterSandboxRouteDriver driver)) driver.enabled = false;
            GameEvents.Noise += OnNoise;
            agentBody.Arrived += OnArrived;
            agentBody.Stuck += OnStuck;
            subscribed = true;
            nextDecision = 0;
            forceRepath = true;
        }

        private void OnDisable()
        {
            if (!subscribed) return;
            GameEvents.Noise -= OnNoise;
            agentBody.Arrived -= OnArrived;
            agentBody.Stuck -= OnStuck;
            subscribed = false;
            if (body != null) agentBody.Stop();
            routeActive = false;
            visibleTarget = null;
            if (pathDisplay != null) pathDisplay.positionCount = 0;
        }

        private void OnNoise(NoiseEvent noise)
        {
            float now = Time.time;
            if (body == null || !Finite(noise.Position) || !Finite(noise.Loudness) || noise.Loudness <= 0 ||
                !Finite(noise.Time) || noise.Time < 0 || noise.Time > now ||
                (noise.Source != null && (noise.Source == gameObject || noise.Source.transform.IsChildOf(transform)))) return;
            double distance = HunterNavPoint.Distance(ToPoint(agentBody.Position), ToPoint(noise.Position));
            if (distance >= noise.Loudness) return;
            // Loudness is an audible radius, not volume. The square-root falloff retains
            // useful evidence in the middle of the radius while confidence reaches zero at its edge.
            float confidence = Mathf.Sqrt(Mathf.Clamp01(1f - (float)(distance / noise.Loudness)));
            if (!memory.Record(new HunterNoiseObservation(noise.Position.x, noise.Position.y, noise.Position.z,
                noise.Type, noise.Time, noise.Loudness, confidence), now)) return;
            AcceptedNoiseCount++;
            LastRuntimeEvent = "Noise observation accepted; next scheduled decision will evaluate it.";
        }

        private void Update()
        {
            if (Time.time < nextDecision) return;
            nextDecision = Time.time + Mathf.Max(0.05f, decisionInterval);
            if (body == null || pathfinder == null || !body.isActiveAndEnabled || !pathfinder.isActiveAndEnabled)
            {
                if (body != null) agentBody.Stop();
                routeActive = false;
                Status = "Enable the referenced AgentBody and HunterNavMeshPathfinder.";
                return;
            }
            EvaluateAndAct();
        }

        private void EvaluateAndAct()
        {
            float now = Time.time;
            LastDecisionTime = now;
            DecisionCount++;
            visibleTarget = PlayerRegistry.Current;
            if (!CanSee(visibleTarget) || IsPursuitBlocked(visibleTarget.Position)) visibleTarget = null;
            if (previouslyVisible && !HasVisibleTarget)
            {
                pendingReason = HunterDecisionReason.TargetLostOrInvalid;
                LastRuntimeEvent = "Target lost, invalid, or temporarily excluded after failed pursuit.";
            }
            else if (!previouslyVisible && HasVisibleTarget) pendingReason = HunterDecisionReason.VisiblePlayer;
            previouslyVisible = HasVisibleTarget;
            bool previouslyRemembered = HasSelectedNoise;
            HasSelectedNoise = memory.TryGetMostRelevant(now, resolvedThroughTime, out HunterRememberedNoise noise);
            if (!HasSelectedNoise) HasSelectedNoise = memory.TryGetMostRelevant(now, out noise);
            SelectedNoise = noise;
            if (previouslyRemembered && !HasSelectedNoise)
            {
                pendingReason = HunterDecisionReason.MemoryExpired;
                LastRuntimeEvent = "Sound memory expired; no stale observation is used.";
            }
            bool fresh = HasSelectedNoise && noise.Observation.Time > resolvedThroughTime &&
                noise.Confidence >= minimumTrailConfidence && noise.EffectiveTrust >= 0.4f && !IsPursuitBlocked(NoisePosition(noise));
            if (fresh && noise.Observation.Time > lastFreshNoiseTime)
            {
                lastFreshNoiseTime = noise.Observation.Time;
                if (!HasVisibleTarget) pendingReason = HunterDecisionReason.TrustedSound;
            }

            if (fresh || HasVisibleTarget)
            {
                trailPosition = HasVisibleTarget ? visibleTarget.Position : NoisePosition(noise);
                hasTrail = true;
                searching = false;
            }
            else if (hasTrail && !searching) BeginSearch(pendingReason == HunterDecisionReason.MemoryExpired ||
                pendingReason == HunterDecisionReason.TargetLostOrInvalid ? pendingReason : HunterDecisionReason.TrailCooling);
            if (searching && now >= searchEnds)
            {
                searching = false;
                hasTrail = false;
                pendingReason = HunterDecisionReason.SearchCompleted;
                LastRuntimeEvent = "Bounded search completed; unresolved old evidence will not restart it.";
            }

            BuildContext(fresh);
            LastSelection = selector.Select(LastContext, CurrentAction);
            // A trail whose fading scores no longer justify pursuit gets one bounded
            // search phase. Consuming its timestamp prevents endlessly rediscovering it.
            if (LastSelection.Action == HunterAction.Wander && fresh && !HasVisibleTarget)
            {
                BeginSearch(HunterDecisionReason.TrailCooling);
                BuildContext(false);
                LastSelection = selector.Select(LastContext, CurrentAction);
            }
            if (LastSelection.Action != CurrentAction)
            {
                transitions.Record(new HunterActionTransition(now, CurrentAction, LastSelection, pendingReason));
                pendingReason = HunterDecisionReason.UtilityScores;
                agentBody.Stop();
                routeActive = false;
                forceRepath = true;
                stuckRetries = 0;
                CurrentAction = LastSelection.Action;
            }

            switch (CurrentAction)
            {
                case HunterAction.Wander: Wander(); break;
                case HunterAction.Investigate: RequestPath(trailPosition, MoveStyle.Walk); break;
                case HunterAction.Chase: RequestPath(trailPosition, MoveStyle.Run); break;
                case HunterAction.Search: Search(); break;
                case HunterAction.Attack: Attack(); break;
            }
        }

        private void BuildContext(bool fresh)
        {
            float proximity = hasTrail ? 1f - Vector3.Distance(agentBody.Position, trailPosition) /
                (HasVisibleTarget ? Mathf.Max(1, sightRange) : Mathf.Max(1, noiseProximityRange)) : 0;
            LastContext = new HunterUtilityContext(
                fresh ? SelectedNoise.SoundIntensity * SelectedNoise.Confidence : 0,
                HasVisibleTarget ? 1 : hasTrail && HasSelectedNoise ? SelectedNoise.Confidence : 0,
                HasVisibleTarget ? 1 : HasSelectedNoise ? SelectedNoise.EffectiveTrust : 0,
                proximity, searching ? 1 : 0, CanAttack(visibleTarget) ? 1 : 0);
        }

        private void Wander()
        {
            if (routeActive || Time.time < nextMove) return;
            Vector3 desired;
            if (wanderPoints != null && wanderPoints.Length > 0 && wanderPoints[wanderIndex % wanderPoints.Length] != null)
                desired = wanderPoints[wanderIndex % wanderPoints.Length].position;
            else desired = home + Quaternion.Euler(0, wanderIndex * 137.5f, 0) * Vector3.forward * 4;
            if (TryGround(desired, out Vector3 grounded)) RequestPath(grounded, MoveStyle.Walk);
            else { wanderIndex++; nextMove = Time.time + 1; Status = "No reachable wander point; waiting before retry."; }
        }

        private void Search()
        {
            if (routeActive || Time.time < nextMove) return;
            Vector3 center = TryGround(trailPosition, out Vector3 grounded) ? grounded : agentBody.Position;
            Vector3 offset = Quaternion.Euler(0, searchIndex * 90, 0) * Vector3.forward * searchRadius;
            if (TryGround(center + offset, out Vector3 point)) RequestPath(point, MoveStyle.Sneak);
            else { searchIndex++; nextMove = Time.time + 0.5f; }
        }

        private void RequestPath(Vector3 goal, MoveStyle style)
        {
            if (Time.time < nextPath || Time.time < nextMove) return;
            if (routeActive && !forceRepath && Vector3.Distance(routeGoal, goal) < movingTargetRepathDistance) return;
            nextPath = Time.time + Mathf.Max(0.1f, minimumRepathInterval);
            forceRepath = false;
            routeGoal = goal;
            HasDestination = true;
            PathRequestCount++;
            if (!routeFinder.TryFindPath(agentBody.Position, goal, path))
            {
                PathFailureCount++;
                routeActive = false;
                agentBody.Stop();
                Status = pathfinder.NavigationStatus;
                LastRuntimeEvent = "A* route failed; retries are throttled.";
                if (CurrentAction == HunterAction.Investigate || CurrentAction == HunterAction.Chase)
                    AbandonPursuit(HunterDecisionReason.UnreachablePursuit);
                else if (CurrentAction == HunterAction.Wander) wanderIndex++;
                else searchIndex++;
                nextMove = nextPath;
                if (pathDisplay != null) pathDisplay.positionCount = 0;
                return;
            }
            routeActive = true;
            Status = $"{CurrentAction}: {path.Count} A* waypoints, {pathfinder.LastSearch.NodesExpanded} nodes expanded.";
            if (pathDisplay != null)
            {
                pathDisplay.positionCount = path.Count;
                for (int i = 0; i < path.Count; i++) pathDisplay.SetPosition(i, path[i] + Vector3.up * 0.08f);
            }
            agentBody.Execute(new MoveCommand(path, style));
        }

        private void OnArrived()
        {
            ArrivalCount++;
            LastRuntimeEvent = "AgentBody reported arrival at the current destination.";
            routeActive = false;
            stuckRetries = 0;
            nextMove = Time.time + 0.5f;
            if (CurrentAction == HunterAction.Wander) wanderIndex++;
            else if (CurrentAction == HunterAction.Search) searchIndex++;
            else if (!HasVisibleTarget) BeginSearch(HunterDecisionReason.ArrivedAtTrail);
            nextDecision = 0;
        }

        private void OnStuck()
        {
            if (Time.time < nextStuckRecovery) return;
            nextStuckRecovery = Time.time + Mathf.Max(0.1f, minimumRepathInterval);
            StuckRecoveryCount++;
            agentBody.Stop();
            routeActive = false;
            forceRepath = true;
            if (stuckRetries == 0) pathfinder.InvalidateGraph();
            nextPath = Time.time + Mathf.Max(0.1f, minimumRepathInterval);
            Status = "AgentBody stuck; delayed A* replan requested.";
            LastRuntimeEvent = "Stuck event accepted; duplicate events are suppressed during the replan cooldown.";
            if (++stuckRetries >= 2)
            {
                if (CurrentAction == HunterAction.Wander) { wanderIndex++; stuckRetries = 0; }
                else if (CurrentAction == HunterAction.Search) { searchIndex++; stuckRetries = 0; }
                else AbandonPursuit(HunterDecisionReason.RepeatedStuck);
            }
        }

        private bool IsPursuitBlocked(Vector3 goal) => Time.time < blockedPursuitUntil &&
            Vector3.Distance(goal, blockedPursuitGoal) <= Mathf.Max(1f, movingTargetRepathDistance);

        private void AbandonPursuit(HunterDecisionReason reason)
        {
            blockedPursuitGoal = routeGoal;
            blockedPursuitUntil = Time.time + Mathf.Max(1f, searchDuration);
            trailPosition = agentBody.Position;
            LastRuntimeEvent = reason == HunterDecisionReason.RepeatedStuck
                ? "Repeated stuck events: pursue this location again only after cooldown or target movement."
                : "Unreachable pursuit: search locally while this destination cools down.";
            BeginSearch(reason);
        }

        private void BeginSearch(HunterDecisionReason reason)
        {
            if (searching) return;
            pendingReason = reason;
            if (HasSelectedNoise) resolvedThroughTime = System.Math.Max(resolvedThroughTime, SelectedNoise.Observation.Time);
            if (!hasTrail) trailPosition = agentBody.Position;
            hasTrail = true;
            searching = true;
            searchEnds = Time.time + Mathf.Max(0.1f, searchDuration);
            searchIndex = 0;
            agentBody.Stop();
            routeActive = false;
            forceRepath = true;
            nextDecision = 0;
        }

        private void Attack()
        {
            // Re-check the current registry entry, visibility and actual 3D range at execution.
            IPlayerState target = PlayerRegistry.Current;
            if (!CanAttack(target))
            {
                agentBody.Stop();
                routeActive = false;
                pendingReason = HunterDecisionReason.TargetLostOrInvalid;
                LastRuntimeEvent = "Attack cancelled: target no longer alive, visible and inside actual range.";
                nextDecision = 0;
                return;
            }
            if (agentBody.IsMoving) agentBody.Stop();
            routeActive = false;
            if (Time.time < nextAttack) return;
            nextAttack = Time.time + Mathf.Max(0.1f, attackCooldown);
            agentBody.PlayAction(AgentAction.Attack);
            AttackCount++;
            IDamageable damageable = target.Transform.GetComponentInChildren<IDamageable>();
            if (damageable != null && !damageable.IsDead && attackDamage > 0)
                damageable.TakeDamage(attackDamage, target.Position, gameObject);
            Status = "Attack executed after alive-target, line-of-sight and range checks.";
        }

        private bool CanAttack(IPlayerState target) => ValidTarget(target) &&
            Vector3.Distance(agentBody.Position, target.Position) <= Mathf.Max(0.1f, attackRange) && CanSee(target);

        private bool CanSee(IPlayerState target)
        {
            if (!ValidTarget(target) || Vector3.Distance(agentBody.Position, target.Position) > sightRange) return false;
            if (!Physics.Linecast(agentBody.Position + Vector3.up * 1.2f, target.Position + Vector3.up * 1.2f,
                out RaycastHit hit, sightMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform == target.Transform || hit.transform.IsChildOf(target.Transform);
        }

        private static bool ValidTarget(IPlayerState target)
        {
            if (target == null || (target is Object obj && obj == null)) return false;
            if (target is Behaviour component && !component.isActiveAndEnabled) return false;
            return target.Transform != null && target.Transform.gameObject.activeInHierarchy && target.IsAlive &&
                Finite(target.HealthNormalized) && target.HealthNormalized > 0 && Finite(target.Position);
        }

        private static bool TryGround(Vector3 point, out Vector3 result)
        {
            result = point;
            if (!Finite(point) || !NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return false;
            result = hit.position;
            return true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < path.Count; i++)
            {
                Gizmos.DrawSphere(path[i] + Vector3.up * 0.1f, 0.1f);
                if (i > 0) Gizmos.DrawLine(path[i - 1] + Vector3.up * 0.1f, path[i] + Vector3.up * 0.1f);
            }
        }

        private static HunterNavPoint ToPoint(Vector3 p) => new HunterNavPoint(p.x, p.y, p.z);
        private static Vector3 NoisePosition(HunterRememberedNoise noise) => new Vector3(noise.Observation.X, noise.Observation.Y, noise.Observation.Z);
        private static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
