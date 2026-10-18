using System.Collections.Generic;
using System.Text;
using BlackoutProtocol.AI.Breacher.Logic;
using BlackoutProtocol.Body;
using BlackoutProtocol.Core;
using BlackoutProtocol.Interaction;
using UnityEngine;

namespace BlackoutProtocol.AI.Breacher
{
    /// <summary>
    /// Connects the pure GOAP planner and executor to the Unity scene.
    /// </summary>
    [RequireComponent(typeof(BreacherPathMover))]
    public sealed class BreacherAgent : MonoBehaviour, IGoapActionRunner
    {
        [Header("Scene Targets")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform doorApproach;
        [SerializeField] private SlidingDoor door;
        [SerializeField] private Transform barricadeApproach;
        [SerializeField] private BreacherBarricade barricade;

        [Header("Action Costs")]
        [SerializeField] private float moveToDoorCost = 2f;
        [SerializeField] private float openDoorCost = 2f;
        [SerializeField] private float moveToPlayerCost = 4f;
        [SerializeField] private float attackCost = 1f;
        [SerializeField] private float moveToBarricadeCost = 3f;
        [SerializeField] private float pushBarricadeCost = 2f;

        [Header("Execution")]
        [SerializeField] private float attackRange = 1.4f;
        [SerializeField] private float attackDuration = 0.6f;
        [SerializeField] private float attackDamage = 25f;
        [SerializeField] private float replanDelay = 0.5f;
        [SerializeField] private float playerReplanDistance = 1.5f;
        [SerializeField] private bool showDebugPanel = true;

        private BreacherPathMover pathMover;
        private IPathfinder pathfinder;
        private IAgentBody agentBody;
        private IDoor doorContract;
        private IPushable pushableContract;
        private PlanExecutor executor;
        private IDamageable playerDamageable;
        private readonly List<Vector3> movementPath = new List<Vector3>();
        private GoapPlanResult currentPlan;
        private float attackTimer;
        private float nextReplanTime;
        private bool currentActionFailed;
        private bool barricadeUnavailable;
        private bool waitingForWorldChange;
        private string debugStatus = "Waiting to start";
        private Vector3 plannedPlayerPosition;

        private static WorldState LocationFacts => WorldState.From(
            WorldFact.AtDoor,
            WorldFact.AtBarricade,
            WorldFact.AtPlayer,
            WorldFact.PlayerInRange,
            WorldFact.InCover);

        private void Awake()
        {
            pathMover = GetComponent<BreacherPathMover>();
            pathfinder = pathMover;
            agentBody = GetComponent<AgentBody>();
            doorContract = door;
            pushableContract = barricade;
            playerDamageable = player != null
                ? player.GetComponentInParent<IDamageable>()
                : null;
            executor = new PlanExecutor(this);
        }

        private void OnEnable()
        {
            GameEvents.LevelChanged += HandleLevelChanged;

            if (agentBody != null)
            {
                agentBody.Stuck += HandleAgentStuck;
            }
        }

        private void OnDisable()
        {
            GameEvents.LevelChanged -= HandleLevelChanged;

            if (agentBody != null)
            {
                agentBody.Stuck -= HandleAgentStuck;
            }
        }

        private void Start()
        {
            if (!ReferencesAreValid())
            {
                enabled = false;
                return;
            }

            CreateAndStartPlan();
        }

        private void Update()
        {
            DetectMovedPlayer();
            executor.Tick();

            if (!waitingForWorldChange &&
                executor.Status == PlanExecutionStatus.NeedsReplan &&
                Time.time >= nextReplanTime)
            {
                Debug.Log(
                    $"Breacher replanning: {executor.ReplanReason}",
                    this);
                CreateAndStartPlan();
                nextReplanTime = Time.time + replanDelay;
            }

            UpdateDebugStatus();
        }

        private void CreateAndStartPlan()
        {
            WorldState state = SenseWorldState();
            WorldState goal = WorldState.From(WorldFact.PlayerDefeated);
            List<GoapAction> actions = CreateAvailableActions();

            currentPlan = GoapPlanner.Plan(state, goal, actions);
            plannedPlayerPosition = player.position;
            executor.StartPlan(currentPlan, state);
            waitingForWorldChange = false;

            if (!currentPlan.Succeeded)
            {
                waitingForWorldChange = true;
                debugStatus = "No valid GOAP plan - waiting safely";
                Debug.LogWarning(debugStatus, this);
                return;
            }

            Debug.Log(BuildPlanText(), this);
        }

        private List<GoapAction> CreateAvailableActions()
        {
            var actions = new List<GoapAction>();
            float doorPathCost = GetMovementCost(
                doorApproach.position,
                moveToDoorCost);
            float barricadePathCost = GetMovementCost(
                barricadeApproach.position,
                moveToBarricadeCost);
            float playerPathCost = GetMovementCost(
                player.position,
                moveToPlayerCost);

            Debug.Log(
                $"Breacher route inputs: " +
                $"doorOpen={doorContract.IsOpen}, " +
                $"doorLocked={doorContract.IsLocked}, " +
                $"doorPath={doorPathCost:F1}, " +
                $"barricadeCleared={barricade.IsCleared}, " +
                $"barricadeUnavailable={barricadeUnavailable}, " +
                $"barricadePath={barricadePathCost:F1}, " +
                $"playerPath={playerPathCost:F1}",
                this);

            if (!doorContract.IsOpen && !doorContract.IsLocked)
            {
                actions.Add(new MoveToAction(
                    "Door",
                    WorldState.Empty,
                    WorldState.From(WorldFact.AtDoor),
                    LocationFacts,
                    doorPathCost));

                actions.Add(new OpenDoorAction(openDoorCost));
            }

            if (!barricade.IsCleared && !barricadeUnavailable)
            {
                actions.Add(new MoveToAction(
                    "Barricade",
                    WorldState.Empty,
                    WorldState.From(WorldFact.AtBarricade),
                    LocationFacts,
                    barricadePathCost));
                actions.Add(new PushBarricadeAction(pushBarricadeCost));
            }

            actions.Add(new MoveToAction(
                "Player",
                WorldState.From(WorldFact.PathToPlayerOpen),
                WorldState.From(
                    WorldFact.AtPlayer,
                    WorldFact.PlayerInRange),
                LocationFacts,
                playerPathCost));
            actions.Add(new AttackAction(attackCost));
            return actions;
        }

        private float GetMovementCost(
            Vector3 destination,
            float fallbackCost)
        {
            return pathMover.TryCalculatePathCost(
                    destination,
                    out float pathCost)
                ? Mathf.Max(0.1f, pathCost)
                : fallbackCost;
        }

        private WorldState SenseWorldState()
        {
            WorldState state = WorldState.Empty;

            if (doorContract.IsOpen)
            {
                state = state
                    .With(WorldFact.DoorOpen)
                    .With(WorldFact.PathToPlayerOpen);
            }

            if (playerDamageable != null && playerDamageable.IsDead)
            {
                state = state.With(WorldFact.PlayerDefeated);
            }

            if (barricade.IsCleared)
            {
                state = state
                    .With(WorldFact.BarricadeCleared)
                    .With(WorldFact.PathToPlayerOpen);
            }

            if (HorizontalDistance(transform.position, doorApproach.position) <=
                0.6f)
            {
                state = state.With(WorldFact.AtDoor);
            }

            if (HorizontalDistance(
                    transform.position,
                    barricadeApproach.position) <= 0.6f)
            {
                state = state.With(WorldFact.AtBarricade);
            }

            if (HorizontalDistance(transform.position, player.position) <=
                attackRange)
            {
                state = state
                    .With(WorldFact.AtPlayer)
                    .With(WorldFact.PlayerInRange);
            }

            return state;
        }

        public void Begin(GoapAction action)
        {
            currentActionFailed = false;

            if (action is MoveToAction moveAction)
            {
                if (!TryResolveDestination(
                        moveAction.DestinationName,
                        out Vector3 destination) ||
                    !pathfinder.TryFindPath(
                        agentBody.Position,
                        destination,
                        movementPath))
                {
                    currentActionFailed = true;
                }
                else
                {
                    Transform lookTarget =
                        moveAction.DestinationName == "Player"
                            ? player
                            : null;
                    agentBody.Execute(new MoveCommand(
                        movementPath,
                        MoveStyle.Run,
                        lookTarget));
                }

                return;
            }

            if (action is OpenDoorAction)
            {
                if (doorContract.IsLocked)
                {
                    currentActionFailed = true;
                }
                else
                {
                    doorContract.Open();
                }

                return;
            }

            if (action is PushBarricadeAction)
            {
                Vector3 pushOffset = player.position -
                    pushableContract.Position;
                pushOffset.y = 0f;

                if (!pushableContract.CanPush ||
                    pushOffset.sqrMagnitude < 0.001f)
                {
                    currentActionFailed = true;
                }
                else
                {
                    pushableContract.Push(
                        pushOffset.normalized,
                        pushBarricadeCost);
                }

                return;
            }

            if (action is AttackAction)
            {
                attackTimer = 0f;
                agentBody.PlayAction(AgentAction.Attack);
            }
        }

        public ActionExecutionStatus Tick(GoapAction action)
        {
            if (currentActionFailed)
            {
                return ActionExecutionStatus.Failed;
            }

            if (action is MoveToAction)
            {
                if (agentBody.HasArrived)
                {
                    return ActionExecutionStatus.Succeeded;
                }

                return agentBody.IsMoving
                    ? ActionExecutionStatus.Running
                    : ActionExecutionStatus.Failed;
            }

            if (action is OpenDoorAction)
            {
                if (doorContract.IsLocked)
                {
                    return ActionExecutionStatus.Failed;
                }

                return doorContract.IsOpen
                    ? ActionExecutionStatus.Succeeded
                    : ActionExecutionStatus.Running;
            }

            if (action is PushBarricadeAction)
            {
                if (barricade.IsBlocked)
                {
                    barricadeUnavailable = true;
                    return ActionExecutionStatus.Failed;
                }

                return barricade.IsCleared
                    ? ActionExecutionStatus.Succeeded
                    : ActionExecutionStatus.Running;
            }

            if (action is AttackAction)
            {
                if (playerDamageable == null)
                {
                    return ActionExecutionStatus.Failed;
                }

                if (playerDamageable.IsDead)
                {
                    return ActionExecutionStatus.Succeeded;
                }

                attackTimer += Time.deltaTime;

                if (attackTimer < attackDuration)
                {
                    return ActionExecutionStatus.Running;
                }

                attackTimer = 0f;
                playerDamageable.TakeDamage(
                    attackDamage,
                    player.position + Vector3.up,
                    gameObject);

                Debug.Log(
                    $"Breacher attacked the player for " +
                    $"{attackDamage:F0} damage.",
                    this);

                if (playerDamageable.IsDead)
                {
                    Debug.Log("Breacher defeated the player.", this);
                    return ActionExecutionStatus.Succeeded;
                }

                agentBody.PlayAction(AgentAction.Attack);
                return ActionExecutionStatus.Running;
            }

            return ActionExecutionStatus.Failed;
        }

        public void Cancel(GoapAction action)
        {
            if (action is MoveToAction)
            {
                agentBody.Stop();
            }
        }

        private bool TryResolveDestination(
            string destinationName,
            out Vector3 destination)
        {
            switch (destinationName)
            {
                case "Door":
                    destination = doorApproach.position;
                    return true;

                case "Barricade":
                    destination = barricadeApproach.position;
                    return true;

                case "Player":
                    Vector3 awayFromPlayer =
                        transform.position - player.position;
                    awayFromPlayer.y = 0f;

                    if (awayFromPlayer.sqrMagnitude < 0.001f)
                    {
                        awayFromPlayer = -player.forward;
                        awayFromPlayer.y = 0f;
                    }

                    destination = player.position +
                        awayFromPlayer.normalized * (attackRange * 0.85f);
                    return true;

                default:
                    destination = transform.position;
                    return false;
            }
        }

        private bool ReferencesAreValid()
        {
            if (pathfinder != null &&
                agentBody != null &&
                doorContract != null &&
                pushableContract != null &&
                playerDamageable != null &&
                player != null &&
                doorApproach != null &&
                door != null &&
                barricadeApproach != null &&
                barricade != null)
            {
                return true;
            }

            debugStatus =
                "Missing AgentBody, IDamageable Player, Door, Barricade, or approach reference";
            Debug.LogError(debugStatus, this);
            return false;
        }

        private void DetectMovedPlayer()
        {
            if (executor.Status != PlanExecutionStatus.Running ||
                !(executor.CurrentAction is MoveToAction moveAction) ||
                moveAction.DestinationName != "Player")
            {
                return;
            }

            if (HorizontalDistance(player.position, plannedPlayerPosition) >=
                playerReplanDistance)
            {
                executor.NotifyPlayerMoved();
            }
        }

        private void HandleLevelChanged(Collider changedObject)
        {
            if (executor != null &&
                executor.Status == PlanExecutionStatus.Running)
            {
                executor.NotifyLevelChanged();
            }
        }

        private void HandleAgentStuck()
        {
            if (executor != null &&
                executor.Status == PlanExecutionStatus.Running)
            {
                executor.NotifyAgentStuck();
            }
        }

        [ContextMenu("Debug/Lock Door And Replan")]
        private void LockDoorAndReplan()
        {
            if (door == null)
            {
                Debug.LogWarning("No door is assigned to the Breacher.", this);
                return;
            }

            door.Lock();

            if (executor != null)
            {
                executor.NotifyLevelChanged();
            }

            Debug.Log(
                "Door locked during execution; Breacher requested a replan.",
                this);
        }

        private void UpdateDebugStatus()
        {
            if (executor == null)
            {
                return;
            }

            string actionName = executor.CurrentAction?.Name ?? "None";
            debugStatus =
                $"Status: {executor.Status}\n" +
                $"Action: {actionName}\n" +
                $"Replan reason: {executor.ReplanReason}";
        }

        private string BuildPlanText()
        {
            var text = new StringBuilder("Breacher GOAP plan:");

            foreach (GoapAction action in currentPlan.Actions)
            {
                text.Append("\n - ")
                    .Append(action.Name)
                    .Append(" (")
                    .Append(action.BaseCost.ToString("F1"))
                    .Append(")");
            }

            text.Append("\nCost: ")
                .Append(currentPlan.TotalCost.ToString("F1"));
            return text.ToString();
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }

        private void OnGUI()
        {
            if (!showDebugPanel)
            {
                return;
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                padding = new RectOffset(12, 12, 10, 10)
            };

            GUI.Box(
                new Rect(12f, Screen.height - 105f, 360f, 90f),
                debugStatus,
                style);
        }

        private void OnValidate()
        {
            moveToDoorCost = Mathf.Max(0.1f, moveToDoorCost);
            openDoorCost = Mathf.Max(0.1f, openDoorCost);
            moveToPlayerCost = Mathf.Max(0.1f, moveToPlayerCost);
            attackCost = Mathf.Max(0.1f, attackCost);
            moveToBarricadeCost = Mathf.Max(0.1f, moveToBarricadeCost);
            pushBarricadeCost = Mathf.Max(0.1f, pushBarricadeCost);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackDuration = Mathf.Max(0f, attackDuration);
            attackDamage = Mathf.Max(0.1f, attackDamage);
            replanDelay = Mathf.Max(0.1f, replanDelay);
            playerReplanDistance = Mathf.Max(0.1f, playerReplanDistance);
        }
    }
}
