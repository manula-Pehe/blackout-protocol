using System.Collections.Generic;
using System.Text;
using BlackoutProtocol.AI.Breacher.Logic;
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

        [Header("Action Costs")]
        [SerializeField] private float moveToDoorCost = 2f;
        [SerializeField] private float openDoorCost = 2f;
        [SerializeField] private float moveToPlayerCost = 4f;
        [SerializeField] private float attackCost = 1f;

        [Header("Execution")]
        [SerializeField] private float attackRange = 1.4f;
        [SerializeField] private float attackDuration = 0.6f;
        [SerializeField] private float replanDelay = 0.5f;
        [SerializeField] private float playerReplanDistance = 1.5f;
        [SerializeField] private bool showDebugPanel = true;

        private BreacherPathMover pathMover;
        private PlanExecutor executor;
        private GoapPlanResult currentPlan;
        private float attackTimer;
        private float nextReplanTime;
        private bool currentActionFailed;
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
            executor = new PlanExecutor(this);
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

            if (executor.Status == PlanExecutionStatus.NeedsReplan &&
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

            if (!currentPlan.Succeeded)
            {
                debugStatus = "No valid GOAP plan";
                Debug.LogWarning(debugStatus, this);
                return;
            }

            Debug.Log(BuildPlanText(), this);
        }

        private List<GoapAction> CreateAvailableActions()
        {
            var actions = new List<GoapAction>();

            if (!door.IsOpen)
            {
                actions.Add(new MoveToAction(
                    "Door",
                    WorldState.Empty,
                    WorldState.From(WorldFact.AtDoor),
                    LocationFacts,
                    moveToDoorCost));

                if (!door.IsLocked)
                {
                    actions.Add(new OpenDoorAction(openDoorCost));
                }
            }

            actions.Add(new MoveToAction(
                "Player",
                WorldState.From(WorldFact.PathToPlayerOpen),
                WorldState.From(
                    WorldFact.AtPlayer,
                    WorldFact.PlayerInRange),
                LocationFacts,
                moveToPlayerCost));
            actions.Add(new AttackAction(attackCost));
            return actions;
        }

        private WorldState SenseWorldState()
        {
            WorldState state = WorldState.Empty;

            if (door.IsOpen)
            {
                state = state
                    .With(WorldFact.DoorOpen)
                    .With(WorldFact.PathToPlayerOpen);
            }

            if (HorizontalDistance(transform.position, doorApproach.position) <=
                0.6f)
            {
                state = state.With(WorldFact.AtDoor);
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
                    !pathMover.MoveTo(destination))
                {
                    currentActionFailed = true;
                }

                return;
            }

            if (action is OpenDoorAction)
            {
                if (door.IsLocked)
                {
                    currentActionFailed = true;
                }
                else
                {
                    door.Open();
                }

                return;
            }

            if (action is AttackAction)
            {
                attackTimer = 0f;
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
                if (!pathMover.LastPathSucceeded)
                {
                    return ActionExecutionStatus.Failed;
                }

                return pathMover.HasReachedDestination
                    ? ActionExecutionStatus.Succeeded
                    : ActionExecutionStatus.Running;
            }

            if (action is OpenDoorAction)
            {
                if (door.IsLocked)
                {
                    return ActionExecutionStatus.Failed;
                }

                return door.IsOpen
                    ? ActionExecutionStatus.Succeeded
                    : ActionExecutionStatus.Running;
            }

            if (action is AttackAction)
            {
                attackTimer += Time.deltaTime;

                if (attackTimer < attackDuration)
                {
                    return ActionExecutionStatus.Running;
                }

                Debug.Log("Breacher attacked the player.", this);
                return ActionExecutionStatus.Succeeded;
            }

            return ActionExecutionStatus.Failed;
        }

        public void Cancel(GoapAction action)
        {
            if (action is MoveToAction)
            {
                pathMover.Stop();
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
            if (player != null && doorApproach != null && door != null)
            {
                return true;
            }

            debugStatus =
                "Missing Player, Door Approach, or Door reference";
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
                text.Append("\n - ").Append(action.Name);
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
            attackRange = Mathf.Max(0.1f, attackRange);
            attackDuration = Mathf.Max(0f, attackDuration);
            replanDelay = Mathf.Max(0.1f, replanDelay);
            playerReplanDistance = Mathf.Max(0.1f, playerReplanDistance);
        }
    }
}
