using System.Collections.Generic;
using BlackoutProtocol.AI.Flanker.Logic;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.AI.Flanker
{
    /// <summary>Perception and action adapter; movement is exclusively delegated through IAgentBody.</summary>
    [RequireComponent(typeof(FlankerHealth), typeof(FlankerNavMeshPathfinder))]
    [DisallowMultipleComponent]
    public sealed class FlankerBrain : MonoBehaviour, IFlankerActions
    {
        [SerializeField, Min(1f)] private float sightRange = 28f;
        [SerializeField, Range(30f, 180f)] private float fieldOfView = 130f;
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.2f;
        [SerializeField, Min(1f)] private float memorySeconds = 8f;
        [SerializeField, Min(1f)] private float attackRange = 12f;
        [SerializeField, Min(0.1f)] private float attackInterval = 1f;
        [SerializeField, Min(0.1f)] private float attackDamage = 10f;
        [SerializeField] private Transform[] patrolPoints = System.Array.Empty<Transform>();
        [SerializeField] private FlankerTactic currentTactic;
        [SerializeField] private bool playerVisible;
        private IAgentBody body;
        private IPathfinder pathfinder;
        private FlankerNavMeshPathfinder navigation;
        private FlankerHealth health;
        private IPlayerState player;
        private IDamageable playerDamageable;
        private readonly FlankerBehaviourTree tree = new FlankerBehaviourTree();
        private readonly FlankerMemory memory = new FlankerMemory();
        private readonly List<Vector3> path = new List<Vector3>(64);
        private readonly List<Vector3> candidatePath = new List<Vector3>(64);
        private CoverPoint claimed;
        private CoverPoint rejected;
        private float rejectUntil;
        private Vector3 home;
        private Vector3 lastForward;
        private Vector3 goal;
        private bool hasCommand;
        private bool topologyDirty;
        private float rebuildAt;
        private float nextDecision;
        private float nextPath;
        private float nextShot;
        private float nextCover;
        private int patrolIndex;

        public FlankerTactic CurrentTactic => tree.Active;
        public CoverPoint ClaimedCover => claimed;

        private void Awake()
        {
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                if (component is IAgentBody candidate) { body = candidate; break; }
            navigation = GetComponent<FlankerNavMeshPathfinder>();
            pathfinder = navigation;
            health = GetComponent<FlankerHealth>();
            home = transform.position;
        }

        private void OnEnable()
        {
            if (body == null)
            {
                Debug.LogError("Flanker requires a component implementing IAgentBody.", this);
                enabled = false;
                return;
            }
            health.Died += Die;
            health.Damaged += Hit;
            body.Stuck += Stuck;
            GameEvents.AlarmRaised += Alarm;
            GameEvents.LevelChanged += LevelChanged;
            PlayerRegistry.CurrentChanged += PlayerChanged;
            PlayerChanged(PlayerRegistry.Current);
            if (health.IsDead) Die();
        }

        private void Start()
        {
            if (!navigation.TrySample(body.Position, out _))
                Debug.LogWarning("Flanker feet are not on the Humanoid NavMesh. Check the spawn height and run Audit Navigation. Movement fails safely until a reachable path exists.", this);
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= Die;
                health.Damaged -= Hit;
            }
            if (body != null) body.Stuck -= Stuck;
            GameEvents.AlarmRaised -= Alarm;
            GameEvents.LevelChanged -= LevelChanged;
            PlayerRegistry.CurrentChanged -= PlayerChanged;
            tree.Abort(this);
            ReleaseCover();
            body?.Stop();
        }

        private void Update()
        {
            if (health.IsDead || Time.time < nextDecision) return;
            nextDecision = Time.time + Mathf.Max(0.05f, decisionInterval);
            if (topologyDirty)
            {
                if (Time.time < rebuildAt) return;
                navigation.InvalidateGraph();
                topologyDirty = false;
            }
            if (player != null && !player.IsAlive)
            {
                tree.Abort(this);
                memory.Clear();
                ReleaseCover();
                body.Stop();
                currentTactic = FlankerTactic.None;
                return;
            }
            Sense();
            bool remembered = memory.HasEvidence(Time.time);
            var facts = new FlankerFacts
            {
                OwnHealth = health.HealthNormalized,
                PlayerVisible = playerVisible,
                PlayerHealth = playerVisible ? player.HealthNormalized : 1f,
                PlayerReloading = playerVisible && player.IsReloading,
                AimedAt = playerVisible && player.AimTarget != null && (player.AimTarget == transform || player.AimTarget.IsChildOf(transform)),
                HasMemory = remembered
            };
            Vector3 threat = remembered ? FlankerNavMeshPathfinder.ToUnity(memory.Position) : home;
            navigation.SetThreat(remembered, threat + Vector3.up * 0.6f, lastForward,
                facts.OwnHealth >= 0.3f && facts.PlayerVisible && (facts.PlayerReloading || facts.PlayerHealth < 0.35f) ? 0.2f : 1f);
            tree.Tick(facts, this);
            currentTactic = tree.Active;
        }

        private void Sense()
        {
            playerVisible = false;
            if (player == null || !player.IsAlive) return;
            Vector3 direction = player.Position + Vector3.up * 0.6f - Eye;
            Vector3 horizontal = direction;
            horizontal.y = 0f;
            if (direction.sqrMagnitude > sightRange * sightRange ||
                Vector3.Angle(transform.forward, horizontal) > fieldOfView * 0.5f ||
                Physics.Linecast(Eye, player.Position + Vector3.up * 0.6f, GameLayers.SightBlockers, QueryTriggerInteraction.Ignore)) return;
            playerVisible = true;
            memory.Remember(FlankerNavMeshPathfinder.ToPoint(player.Position), Time.time, memorySeconds);
            lastForward = player.Transform.forward;
        }

        private Vector3 Eye => body.Position + Vector3.up * 1.6f;
        private Transform LookTarget => playerVisible && player != null ? player.Transform : null;

        public BehaviourStatus Tick(FlankerTactic tactic)
        {
            switch (tactic)
            {
                case FlankerTactic.Retreat:
                    if (memory.HasEvidence(Time.time))
                    {
                        if (TakeCover(true, false)) return BehaviourStatus.Running;
                        Vector3 away = body.Position - FlankerNavMeshPathfinder.ToUnity(memory.Position);
                        away.y = 0f;
                        if (away.sqrMagnitude > 0.01f && Move(body.Position + away.normalized * 5f, MoveStyle.Run) != BehaviourStatus.Failure)
                            return BehaviourStatus.Running;
                    }
                    body.Execute(MoveCommand.Hold(LookTarget));
                    return BehaviourStatus.Running;
                case FlankerTactic.Punish:
                    if (Shoot()) return BehaviourStatus.Running;
                    return Move(PlayerFeet, MoveStyle.Run);
                case FlankerTactic.Hold:
                    if (!TakeCover(false, true)) body.Execute(MoveCommand.Hold(LookTarget));
                    return BehaviourStatus.Running;
                case FlankerTactic.Flank:
                    if (!TakeCover(false, false)) return Shoot() ? BehaviourStatus.Running : BehaviourStatus.Failure;
                    if (claimed != null && HorizontalDistance(body.Position, claimed.Position) < 0.8f) Shoot();
                    return BehaviourStatus.Running;
                case FlankerTactic.Investigate:
                    if (!memory.HasEvidence(Time.time)) return BehaviourStatus.Failure;
                    Vector3 destination = FlankerNavMeshPathfinder.ToUnity(memory.Position);
                    destination.y = body.Position.y;
                    BehaviourStatus result = Move(destination, MoveStyle.Sneak);
                    if (result == BehaviourStatus.Success) memory.Clear();
                    return result;
                default:
                    Vector3 patrol = PatrolDestination();
                    BehaviourStatus patrolResult = Move(patrol, MoveStyle.Walk);
                    if (patrolResult != BehaviourStatus.Running) patrolIndex++;
                    return patrolResult;
            }
        }

        public void Abort(FlankerTactic tactic)
        {
            ReleaseCover();
            hasCommand = false;
            nextPath = 0f;
            body?.Stop();
        }

        private Vector3 PlayerFeet
        {
            get
            {
                Vector3 position = player.Position;
                position.y = body.Position.y;
                return position;
            }
        }

        private BehaviourStatus Move(Vector3 destination, MoveStyle style)
        {
            if (HorizontalDistance(body.Position, destination) < 0.65f)
            {
                body.Execute(MoveCommand.Hold(LookTarget));
                return BehaviourStatus.Success;
            }
            float displacement = HorizontalDistance(goal, destination);
            if (FlankerRoutePolicy.KeepCurrent(hasCommand, body.IsMoving, displacement,
                displacement >= 1.5f && NearRouteCorner(), Time.time, nextPath)) return BehaviourStatus.Running;
            if (Time.time < nextPath)
                return hasCommand && body.IsMoving && HorizontalDistance(goal, destination) < 1.5f ? BehaviourStatus.Running : BehaviourStatus.Failure;
            nextPath = Time.time + 0.75f;
            if (!pathfinder.TryFindPath(body.Position, destination, path))
            {
                body.Stop();
                hasCommand = false;
                return BehaviourStatus.Failure;
            }
            goal = destination;
            hasCommand = true;
            body.Execute(new MoveCommand(path, style, LookTarget));
            return BehaviourStatus.Running;
        }

        private bool NearRouteCorner()
        {
            for (int index = 1; index < path.Count; index++)
                if (HorizontalDistance(body.Position, path[index]) < 0.75f) return true;
            return false;
        }

        private bool TakeCover(bool retreat, bool hold)
        {
            if (claimed != null && (!claimed.isActiveAndEnabled || !Protected(claimed))) ReleaseCover();
            if (claimed != null && !retreat && !hold)
            {
                Vector3 direction = claimed.Position - FlankerNavMeshPathfinder.ToUnity(memory.Position);
                direction.y = 0f;
                if (Vector3.Dot(lastForward.normalized, direction.normalized) > 0.5f) ReleaseCover();
            }
            if (claimed == null)
            {
                if (Time.time < nextCover) return false;
                nextCover = Time.time + 1f;
                CoverPoint selected = null;
                double best = double.NegativeInfinity;
                Vector3 threat = FlankerNavMeshPathfinder.ToUnity(memory.Position);
                IReadOnlyList<CoverPoint> points = CoverPoint.All;
                for (int index = 0; index < points.Count; index++)
                {
                    CoverPoint candidate = points[index];
                    if (!candidate.IsFree || (candidate == rejected && Time.time < rejectUntil) || !Protected(candidate)) continue;
                    Vector3 direction = candidate.Position - threat;
                    direction.y = 0f;
                    float facing = lastForward.sqrMagnitude < 0.01f ? 0f : Vector3.Dot(lastForward.normalized, direction.normalized);
                    if (!retreat && !hold && facing > 0.5f) continue;
                    if (retreat && direction.magnitude < HorizontalDistance(body.Position, threat) + 1f) continue;
                    if (!pathfinder.TryFindPath(body.Position, candidate.Position, candidatePath)) continue;
                    double length = 0d;
                    for (int waypoint = 1; waypoint < candidatePath.Count; waypoint++)
                        length += Vector3.Distance(candidatePath[waypoint - 1], candidatePath[waypoint]);
                    double score = FlankerCoverScoring.Score(true, true, Mathf.Clamp01(candidate.CoverHeight / 1.5f),
                        length, hold ? 0f : facing, direction.magnitude, retreat);
                    if (score <= best) continue;
                    best = score;
                    selected = candidate;
                }
                if (selected == null || !selected.TryClaim(gameObject)) return false;
                claimed = selected;
            }
            BehaviourStatus result = Move(claimed.Position, retreat ? MoveStyle.Run : MoveStyle.Strafe);
            if (result != BehaviourStatus.Failure) return true;
            rejected = claimed;
            rejectUntil = Time.time + 2f;
            ReleaseCover();
            return false;
        }

        private bool Protected(CoverPoint point)
        {
            if (!memory.HasEvidence(Time.time)) return false;
            Vector3 threatEye = FlankerNavMeshPathfinder.ToUnity(memory.Position) + Vector3.up * 0.6f;
            return Physics.Linecast(point.Position + Vector3.up * 0.9f, threatEye, GameLayers.SightBlockers, QueryTriggerInteraction.Ignore);
        }

        private bool Shoot()
        {
            if (!playerVisible || player == null || !player.IsAlive || playerDamageable == null || playerDamageable.IsDead ||
                Vector3.Distance(Eye, player.Position + Vector3.up * 0.6f) > attackRange ||
                Physics.Linecast(Eye, player.Position + Vector3.up * 0.6f, GameLayers.SightBlockers, QueryTriggerInteraction.Ignore)) return false;
            body.Execute(MoveCommand.Hold(LookTarget));
            if (Time.time < nextShot) return true;
            nextShot = Time.time + Mathf.Max(0.1f, attackInterval);
            body.PlayAction(AgentAction.Attack);
            playerDamageable.TakeDamage(attackDamage, player.Position, gameObject);
            GameEvents.RaiseNoise(new NoiseEvent(Eye, NoiseLevels.Gunshot, NoiseType.Gunshot, Time.time, gameObject));
            return true;
        }

        private Vector3 PatrolDestination()
        {
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                Transform point = patrolPoints[patrolIndex % patrolPoints.Length];
                if (point != null) return point.position;
            }
            return home + ((patrolIndex % 2 == 0) ? Vector3.right : Vector3.left) * 3f;
        }

        private void ReleaseCover()
        {
            if (claimed != null) claimed.Release(gameObject);
            claimed = null;
        }

        private void PlayerChanged(IPlayerState current)
        {
            tree.Abort(this);
            ReleaseCover();
            memory.Clear();
            player = current;
            playerDamageable = current != null ? current.Transform.GetComponentInParent<IDamageable>() : null;
            playerVisible = false;
            nextDecision = 0f;
        }

        private void Alarm(Vector3 position)
        {
            if (!FlankerPoint.Finite(position.x) || !FlankerPoint.Finite(position.y) || !FlankerPoint.Finite(position.z)) return;
            memory.Remember(FlankerNavMeshPathfinder.ToPoint(position), Time.time, memorySeconds);
            lastForward = Vector3.zero;
            nextDecision = 0f;
        }

        private void LevelChanged(Collider changed)
        {
            body.Stop();
            ReleaseCover();
            hasCommand = false;
            topologyDirty = true;
            rebuildAt = Time.time + 0.65f;
            nextPath = rebuildAt;
        }

        private void Stuck()
        {
            rejected = claimed;
            rejectUntil = Time.time + 3f;
            LevelChanged(null);
        }

        private void Hit()
        {
            body.PlayAction(AgentAction.Hit);
            nextDecision = 0f;
        }

        private void Die()
        {
            tree.Abort(this);
            ReleaseCover();
            memory.Clear();
            body.Stop();
            body.PlayAction(AgentAction.Die);
            if (body is Behaviour component) component.enabled = false;
            Collider collision = GetComponent<Collider>();
            if (collision != null) collision.enabled = false;
            currentTactic = FlankerTactic.None;
            enabled = false;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
