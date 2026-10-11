using System.Collections.Generic;
using System.Text;
using BlackoutProtocol.AI.Breacher.Logic;
using UnityEngine;

namespace BlackoutProtocol.AI.Breacher
{
    /// <summary>
    /// Runs configurable sandbox plans and displays the planner's evidence.
    /// </summary>
    public sealed class BreacherPlannerDebug : MonoBehaviour
    {
        [Header("Initial World")]
        [SerializeField] private bool doorStartsOpen;
        [SerializeField] private bool doorLocked;
        [SerializeField] private bool barricadePushable = true;
        [SerializeField] private bool lowHealth;

        [Header("Action Costs")]
        [SerializeField] private float moveToDoorCost = 2f;
        [SerializeField] private float openDoorCost = 2f;
        [SerializeField] private float moveToBarricadeCost = 3f;
        [SerializeField] private float pushBarricadeCost = 2f;
        [SerializeField] private float moveToPlayerCost = 4f;
        [SerializeField] private float attackCost = 1f;
        [SerializeField] private float takeCoverCost = 2f;

        [Header("Search")]
        [SerializeField] private int maximumExpandedStates = 128;
        [SerializeField] private bool showDebugPanel = true;

        private GoapPlanResult currentPlan;
        private string currentGoal;
        private string debugText = "No plan created.";

        private static WorldState LocationFacts => WorldState.From(
            WorldFact.AtDoor,
            WorldFact.AtBarricade,
            WorldFact.AtPlayer,
            WorldFact.PlayerInRange,
            WorldFact.InCover);

        private void Start()
        {
            CreatePlan();
        }

        [ContextMenu("Create Plan")]
        public void CreatePlan()
        {
            WorldState initialState = CreateInitialState();
            WorldState goal = CreateGoal();
            List<GoapAction> actions = CreateAvailableActions();

            currentPlan = GoapPlanner.Plan(
                initialState,
                goal,
                actions,
                maximumExpandedStates);

            debugText = BuildDebugText();
            Debug.Log(debugText, this);
        }

        private WorldState CreateInitialState()
        {
            WorldState state = WorldState.Empty;

            if (doorStartsOpen)
            {
                state = state
                    .With(WorldFact.DoorOpen)
                    .With(WorldFact.PathToPlayerOpen);
            }

            if (lowHealth)
            {
                state = state.With(WorldFact.LowHealth);
            }

            return state;
        }

        private WorldState CreateGoal()
        {
            if (lowHealth)
            {
                currentGoal = "Recover In Cover";
                return WorldState.From(WorldFact.InCover);
            }

            currentGoal = "Reach And Attack Player";
            return WorldState.From(WorldFact.PlayerDefeated);
        }

        private List<GoapAction> CreateAvailableActions()
        {
            var actions = new List<GoapAction>();

            if (!doorStartsOpen && !doorLocked)
            {
                actions.Add(new MoveToAction(
                    "Door",
                    WorldState.Empty,
                    WorldState.From(WorldFact.AtDoor),
                    LocationFacts,
                    moveToDoorCost));
                actions.Add(new OpenDoorAction(openDoorCost));
            }

            if (!doorStartsOpen && barricadePushable)
            {
                actions.Add(new MoveToAction(
                    "Barricade",
                    WorldState.Empty,
                    WorldState.From(WorldFact.AtBarricade),
                    LocationFacts,
                    moveToBarricadeCost));
                actions.Add(new PushBarricadeAction(pushBarricadeCost));
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
            actions.Add(new TakeCoverAction(takeCoverCost));

            return actions;
        }

        private string BuildDebugText()
        {
            var text = new StringBuilder();
            text.AppendLine("BREACHER GOAP DEBUG");
            text.AppendLine($"Goal: {currentGoal}");

            if (currentPlan == null || !currentPlan.Succeeded)
            {
                text.AppendLine("Plan: NO VALID PLAN");
                text.AppendLine(
                    $"Expanded: {currentPlan?.ExpandedStateCount ?? 0}");
                return text.ToString();
            }

            text.AppendLine("Plan:");

            for (int index = 0; index < currentPlan.Actions.Count; index++)
            {
                GoapAction action = currentPlan.Actions[index];
                text.AppendLine(
                    $"  {index + 1}. {action.Name} " +
                    $"(cost {action.BaseCost:F1})");
            }

            text.AppendLine($"Total cost: {currentPlan.TotalCost:F1}");
            text.AppendLine($"Expanded: {currentPlan.ExpandedStateCount}");
            text.AppendLine(
                $"Time: {currentPlan.ElapsedMilliseconds:F3} ms");
            return text.ToString();
        }

        private void OnGUI()
        {
            if (!showDebugPanel)
            {
                return;
            }

            int lineCount = currentPlan != null && currentPlan.Succeeded
                ? currentPlan.Actions.Count + 6
                : 4;
            float height = lineCount * 22f + 20f;

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                padding = new RectOffset(12, 12, 10, 10)
            };

            GUI.Box(
                new Rect(12f, 12f, 380f, height),
                debugText,
                style);
        }

        private void OnValidate()
        {
            moveToDoorCost = Mathf.Max(0.1f, moveToDoorCost);
            openDoorCost = Mathf.Max(0.1f, openDoorCost);
            moveToBarricadeCost = Mathf.Max(0.1f, moveToBarricadeCost);
            pushBarricadeCost = Mathf.Max(0.1f, pushBarricadeCost);
            moveToPlayerCost = Mathf.Max(0.1f, moveToPlayerCost);
            attackCost = Mathf.Max(0.1f, attackCost);
            takeCoverCost = Mathf.Max(0.1f, takeCoverCost);
            maximumExpandedStates = Mathf.Max(1, maximumExpandedStates);
        }
    }
}
