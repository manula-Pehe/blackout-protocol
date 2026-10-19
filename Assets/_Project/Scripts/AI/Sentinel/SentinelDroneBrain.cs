using System.Collections.Generic;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.AI.Sentinel
{
    /// <summary>
    /// Unity-facing perception and navigation adapter for the Sentinel HSM.
    /// Assign child transforms under Patrol Route in patrol order.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SentinelDroneBody))]
    public sealed class SentinelDroneBrain : MonoBehaviour
    {
        [Header("Patrol")]
        [SerializeField] private Transform patrolRouteRoot;
        [SerializeField, Min(1f)] private float fallbackPatrolRadius = 4f;

        [Header("Perception")]
        [SerializeField] private Transform sensorOrigin;
        [SerializeField, Min(1f)] private float sightRange = 18f;
        [SerializeField, Range(1f, 179f)] private float fieldOfView = 105f;
        [SerializeField, Min(0.02f)] private float perceptionInterval = 0.12f;
        [SerializeField] private LayerMask sightMask = ~0;

        [Header("Alert and tracking")]
        [SerializeField, Min(0f)] private float alertDuration = 0.55f;
        [SerializeField, Min(1f)] private float observationDistance = 5.5f;
        [SerializeField, Min(0.1f)] private float observationRefreshInterval = 1.1f;
        [SerializeField, Min(0.1f)] private float playerPositionRefreshDistance = 1.4f;

        [Header("Search")]
        [SerializeField, Min(0.1f)] private float predictionSeconds = 1.1f;
        [SerializeField, Min(0.5f)] private float searchRadius = 3f;
        [SerializeField, Min(0f)] private float searchDwell = 0.65f;
        [SerializeField, Min(1)] private int searchPointCount = 5;

        private readonly List<Vector3> patrolPoints = new List<Vector3>(8);
        private readonly List<float> patrolVisitTimes = new List<float>(8);
        private readonly List<Vector3> candidatePath = new List<Vector3>(32);
        private readonly List<Vector3> selectedPath = new List<Vector3>(32);
        private readonly RaycastHit[] rayHits = new RaycastHit[16];

        private SentinelHsm hsm;
        private SentinelNavMeshPathfinder pathfinder;
        private IAgentBody body;
        private IPlayerState player;
        private Vector3 lastKnownPosition;
        private Vector3 lastKnownHeading;
        private Vector3 plannedPlayerPosition;
        private float nextSenseTime;
        private float nextObservationTime;
        private float searchArrivalTime = -1f;
        private int currentPatrolIndex = -1;
        private int searchIndex;
        private int returningToIndex = -1;
        private bool playerVisible;
        private bool routeInitialized;
        private bool bodyStuck;
        private bool hasLastKnownPosition;
        private bool returnFailed;
        private bool searchPathFailed;

        /// <summary>The current HSM leaf state, useful for inspector/debug tooling.</summary>
        public SentinelState State => hsm != null ? hsm.State : SentinelState.Patrol;

        private void Awake()
        {
            body = GetComponent<SentinelDroneBody>();
            pathfinder = new SentinelNavMeshPathfinder();
            hsm = new SentinelHsm();
            if (sensorOrigin == null)
            {
                Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < childTransforms.Length; i++)
                {
                    if (childTransforms[i].name == "Camera_Aim_Pivot")
                    {
                        sensorOrigin = childTransforms[i];
                        break;
                    }
                }
                if (sensorOrigin == null)
                {
                    sensorOrigin = transform;
                }
            }
        }

        private void OnEnable()
        {
            if (hsm != null)
            {
                hsm.StateChanged += HandleStateChanged;
            }
            if (body != null)
            {
                body.Stuck += HandleBodyStuck;
            }
            if (hsm != null)
            {
                hsm.Reset();
            }
            GameEvents.LevelChanged += HandleLevelChanged;
        }

        private void Start()
        {
            if (body == null)
            {
                Debug.LogError("SentinelDroneBrain requires SentinelDroneBody on the same object.", this);
                enabled = false;
                return;
            }

            pathfinder.Rebuild();
            CachePatrolRoute();
            EnterPatrol();
        }

        private void OnDisable()
        {
            GameEvents.LevelChanged -= HandleLevelChanged;
            if (hsm != null)
            {
                hsm.StateChanged -= HandleStateChanged;
            }
            if (body != null)
            {
                body.Stuck -= HandleBodyStuck;
            }
            body?.Stop();
        }

        private void Update()
        {
            if (body == null)
            {
                return;
            }

            player = PlayerRegistry.Current;
            if (Time.time >= nextSenseTime)
            {
                nextSenseTime = Time.time + perceptionInterval;
                playerVisible = CanSeePlayer(player);
                if (playerVisible)
                {
                    RememberPlayer(player);
                }
            }

            bool searchComplete = UpdateSearch();
            bool returnComplete = hsm.State == SentinelState.Return && (body.HasArrived || returnFailed);
            var input = new SentinelHsmInput(
                playerVisible,
                hsm.StateElapsed >= alertDuration,
                searchComplete,
                returnComplete);
            hsm.Tick(input, Time.deltaTime);

            if (bodyStuck)
            {
                bodyStuck = false;
                ReplanCurrentState();
            }

            switch (hsm.State)
            {
                case SentinelState.Patrol:
                    if (body.HasArrived)
                    {
                        SelectPatrolWaypoint();
                    }
                    break;
                case SentinelState.Track:
                    UpdateTracking();
                    break;
                case SentinelState.Search:
                    AdvanceSearchIfReady();
                    break;
            }
        }

        private bool CanSeePlayer(IPlayerState target)
        {
            if (target == null || !target.IsAlive || target.Transform == null)
            {
                return false;
            }

            Vector3 toPlayer = target.Position - sensorOrigin.position;
            float distance = toPlayer.magnitude;
            if (distance > sightRange || distance < 0.001f)
            {
                return false;
            }

            Vector3 horizontalForward = transform.forward;
            horizontalForward.y = 0f;
            Vector3 horizontalDirection = toPlayer;
            horizontalDirection.y = 0f;
            if (horizontalDirection.sqrMagnitude > 0.001f &&
                Vector3.Angle(horizontalForward, horizontalDirection) > fieldOfView * 0.5f)
            {
                return false;
            }

            int hitCount = Physics.RaycastNonAlloc(
                sensorOrigin.position,
                toPlayer / distance,
                rayHits,
                distance,
                sightMask,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            Transform nearest = null;
            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = rayHits[i].transform;
                if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }
                if (rayHits[i].distance < nearestDistance)
                {
                    nearestDistance = rayHits[i].distance;
                    nearest = hitTransform;
                }
            }

            return nearest == null || IsPlayerTransform(nearest, target.Transform);
        }

        private void RememberPlayer(IPlayerState target)
        {
            lastKnownPosition = target.Position;
            Vector3 heading = target.Velocity;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.09f)
            {
                lastKnownHeading = heading.normalized;
            }
            else if (!hasLastKnownPosition)
            {
                Vector3 towardDrone = transform.position - target.Position;
                towardDrone.y = 0f;
                lastKnownHeading = towardDrone.sqrMagnitude > 0.01f ? towardDrone.normalized : Vector3.forward;
            }
            hasLastKnownPosition = true;
        }

        private void HandleStateChanged(SentinelState previous, SentinelState next)
        {
            switch (next)
            {
                case SentinelState.Patrol:
                    EnterPatrol();
                    break;
                case SentinelState.Alert:
                    body.Stop();
                    body.PlayAction(AgentAction.Alert);
                    if (hasLastKnownPosition)
                    {
                        GameEvents.RaiseAlarm(lastKnownPosition);
                    }
                    break;
                case SentinelState.Track:
                    nextObservationTime = 0f;
                    UpdateTracking(true);
                    break;
                case SentinelState.Search:
                    searchIndex = 0;
                    searchArrivalTime = -1f;
                    MoveToSearchPoint();
                    break;
                case SentinelState.Return:
                    returningToIndex = currentPatrolIndex;
                    if (returningToIndex < 0 || returningToIndex >= patrolPoints.Count)
                    {
                        returningToIndex = FindNearestPatrolPoint();
                    }
                    returnFailed = !MoveToPatrolPoint(returningToIndex);
                    break;
            }
        }

        private void EnterPatrol()
        {
            if (!routeInitialized)
            {
                CachePatrolRoute();
            }
            if (currentPatrolIndex < 0 || currentPatrolIndex >= patrolPoints.Count)
            {
                SelectPatrolWaypoint();
            }
            else
            {
                MoveToPatrolPoint(currentPatrolIndex);
            }
        }

        private void CachePatrolRoute()
        {
            patrolPoints.Clear();
            patrolVisitTimes.Clear();
            if (patrolRouteRoot != null)
            {
                for (int i = 0; i < patrolRouteRoot.childCount; i++)
                {
                    Transform point = patrolRouteRoot.GetChild(i);
                    if (point.gameObject.activeInHierarchy)
                    {
                        patrolPoints.Add(point.position);
                        patrolVisitTimes.Add(-1000f);
                    }
                }
            }
            if (patrolPoints.Count == 0)
            {
                Vector3 origin = transform.position;
                patrolVisitTimes.Clear();
                patrolPoints.Add(origin + transform.right * fallbackPatrolRadius);
                patrolPoints.Add(origin + transform.forward * fallbackPatrolRadius);
                patrolPoints.Add(origin - transform.right * fallbackPatrolRadius);
                patrolPoints.Add(origin - transform.forward * fallbackPatrolRadius);
                for (int i = 0; i < patrolPoints.Count; i++)
                {
                    patrolVisitTimes.Add(-1000f);
                }
            }
            routeInitialized = true;
        }

        private void SelectPatrolWaypoint()
        {
            float bestScore = float.NegativeInfinity;
            int bestIndex = -1;
            for (int i = 0; i < patrolPoints.Count; i++)
            {
                if (i == currentPatrolIndex && patrolPoints.Count > 1)
                {
                    continue;
                }
                if (!TryPath(patrolPoints[i], candidatePath))
                {
                    continue;
                }

                float routeLength = PathLength(candidatePath);
                float recency = Time.time - patrolVisitTimes[i];
                float score = recency - routeLength * 0.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                currentPatrolIndex = bestIndex;
                patrolVisitTimes[bestIndex] = Time.time;
                ExecutePath(patrolPoints[bestIndex], MoveStyle.Walk, null);
            }
            else
            {
                body.Stop();
            }
        }

        private bool MoveToPatrolPoint(int index)
        {
            if (index < 0 || index >= patrolPoints.Count)
            {
                body.Stop();
                return false;
            }
            return ExecutePath(patrolPoints[index], MoveStyle.Walk, null);
        }

        private int FindNearestPatrolPoint()
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < patrolPoints.Count; i++)
            {
                float distance = (patrolPoints[i] - transform.position).sqrMagnitude;
                if (distance < bestDistance && TryPath(patrolPoints[i], candidatePath))
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        private void UpdateTracking(bool force = false)
        {
            if (!playerVisible || player == null || player.Transform == null)
            {
                return;
            }
            if (!force && Time.time < nextObservationTime &&
                (player.Position - plannedPlayerPosition).sqrMagnitude < playerPositionRefreshDistance * playerPositionRefreshDistance)
            {
                return;
            }

            nextObservationTime = Time.time + observationRefreshInterval;
            plannedPlayerPosition = player.Position;
            float preferredDistance = IsPlayerAimingAtDrone(player) ? observationDistance * 1.45f : observationDistance;
            float bestScore = float.PositiveInfinity;
            bool found = false;
            selectedPath.Clear();

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 candidate = player.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * preferredDistance;
                if (!TryPath(candidate, candidatePath))
                {
                    continue;
                }

                float candidateDistance = Vector3.Distance(candidatePath[candidatePath.Count - 1], player.Position);
                float score = Mathf.Abs(candidateDistance - preferredDistance) + PathLength(candidatePath) * 0.18f;
                bool visibleFromCandidate = HasClearView(candidatePath[candidatePath.Count - 1] + Vector3.up * 2f, player.Position);
                if (!visibleFromCandidate)
                {
                    score += 4f;
                }
                if (IsPlayerAimingAtDrone(player) && candidateDistance < observationDistance * 1.2f)
                {
                    score += 6f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    selectedPath.Clear();
                    selectedPath.AddRange(candidatePath);
                    found = true;
                }
            }

            if (found)
            {
                body.Execute(new MoveCommand(selectedPath, MoveStyle.Strafe, player.Transform));
            }
            else
            {
                body.Stop();
            }
        }

        private bool UpdateSearch()
        {
            if (hsm.State != SentinelState.Search)
            {
                return false;
            }
            if (searchPathFailed)
            {
                return true;
            }
            if (searchIndex < searchPointCount || !body.HasArrived)
            {
                return false;
            }
            if (searchArrivalTime < 0f)
            {
                searchArrivalTime = Time.time;
            }
            return Time.time - searchArrivalTime >= searchDwell;
        }

        private void AdvanceSearchIfReady()
        {
            if (!body.HasArrived)
            {
                searchArrivalTime = -1f;
                return;
            }
            if (searchArrivalTime < 0f)
            {
                searchArrivalTime = Time.time;
                return;
            }
            if (Time.time - searchArrivalTime < searchDwell)
            {
                return;
            }

            searchIndex++;
            searchArrivalTime = -1f;
            if (searchIndex < searchPointCount)
            {
                MoveToSearchPoint();
            }
        }

        private void MoveToSearchPoint()
        {
            Vector3 forward = lastKnownHeading.sqrMagnitude > 0.01f ? lastKnownHeading : transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 predicted = lastKnownPosition + forward * predictionSeconds;
            Vector3 target;
            switch (searchIndex % 5)
            {
                case 0: target = predicted; break;
                case 1: target = predicted + right * searchRadius; break;
                case 2: target = predicted - right * searchRadius; break;
                case 3: target = predicted + forward * searchRadius; break;
                default: target = lastKnownPosition; break;
            }
            searchPathFailed = !ExecutePath(target, MoveStyle.Sneak, null);
        }

        private bool ExecutePath(Vector3 goal, MoveStyle style, Transform lookTarget)
        {
            if (!TryPath(goal, candidatePath))
            {
                body.Stop();
                return false;
            }
            body.Execute(new MoveCommand(candidatePath, style, lookTarget));
            return true;
        }

        private bool TryPath(Vector3 goal, List<Vector3> result)
        {
            return pathfinder.TryFindPath(transform.position, goal, result);
        }

        private static float PathLength(IReadOnlyList<Vector3> path)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                length += Vector3.Distance(path[i - 1], path[i]);
            }
            return length;
        }

        private bool HasClearView(Vector3 origin, Vector3 target)
        {
            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            if (distance < 0.001f)
            {
                return true;
            }
            int count = Physics.RaycastNonAlloc(origin, direction / distance, rayHits, distance, sightMask, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            Transform nearest = null;
            for (int i = 0; i < count; i++)
            {
                Transform hitTransform = rayHits[i].transform;
                if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }
                if (rayHits[i].distance < nearestDistance)
                {
                    nearestDistance = rayHits[i].distance;
                    nearest = hitTransform;
                }
            }
            return nearest == null || (player != null && IsPlayerTransform(nearest, player.Transform));
        }

        private bool IsPlayerAimingAtDrone(IPlayerState target)
        {
            Transform aim = target != null ? target.AimTarget : null;
            return aim != null && (aim == transform || aim.IsChildOf(transform));
        }

        private static bool IsPlayerTransform(Transform candidate, Transform playerTransform)
        {
            return candidate == playerTransform || candidate.IsChildOf(playerTransform);
        }

        private void ReplanCurrentState()
        {
            switch (hsm.State)
            {
                case SentinelState.Patrol:
                    SelectPatrolWaypoint();
                    break;
                case SentinelState.Track:
                    UpdateTracking(true);
                    break;
                case SentinelState.Search:
                    MoveToSearchPoint();
                    break;
                case SentinelState.Return:
                    returnFailed = !MoveToPatrolPoint(returningToIndex);
                    break;
            }
        }

        private void HandleLevelChanged(Collider changedObject)
        {
            pathfinder.Rebuild();
            ReplanCurrentState();
        }

        private void HandleBodyStuck()
        {
            bodyStuck = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, sightRange);
            if (patrolRouteRoot != null)
            {
                Gizmos.color = new Color(0.1f, 0.85f, 0.95f, 1f);
                for (int i = 0; i < patrolRouteRoot.childCount; i++)
                {
                    Transform point = patrolRouteRoot.GetChild(i);
                    Gizmos.DrawWireSphere(point.position, 0.2f);
                    if (i > 0)
                    {
                        Gizmos.DrawLine(patrolRouteRoot.GetChild(i - 1).position, point.position);
                    }
                }
            }
            if (hasLastKnownPosition)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(lastKnownPosition, 0.35f);
            }
        }

        private void OnValidate()
        {
            sightRange = Mathf.Max(1f, sightRange);
            perceptionInterval = Mathf.Max(0.02f, perceptionInterval);
            searchPointCount = Mathf.Max(1, searchPointCount);
            fallbackPatrolRadius = Mathf.Max(1f, fallbackPatrolRadius);
        }
    }
}
