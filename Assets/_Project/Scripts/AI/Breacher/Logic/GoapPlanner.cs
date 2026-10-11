using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BlackoutProtocol.AI.Breacher.Logic
{
    /// <summary>
    /// An ordered GOAP plan and measurements from the planning search.
    /// </summary>
    public sealed class GoapPlanResult
    {
        public GoapPlanResult(
            bool succeeded,
            IReadOnlyList<GoapAction> actions,
            float totalCost,
            int expandedStateCount,
            double elapsedMilliseconds)
        {
            Succeeded = succeeded;
            Actions = actions;
            TotalCost = totalCost;
            ExpandedStateCount = expandedStateCount;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public bool Succeeded { get; }
        public IReadOnlyList<GoapAction> Actions { get; }
        public float TotalCost { get; }
        public int ExpandedStateCount { get; }
        public double ElapsedMilliseconds { get; }
    }

    /// <summary>
    /// Uses A* over WorldState values to find a minimum-cost action sequence.
    /// </summary>
    public static class GoapPlanner
    {
        private sealed class OpenRecord
        {
            public OpenRecord(
                WorldState state,
                float costSoFar,
                float estimatedTotalCost)
            {
                State = state;
                CostSoFar = costSoFar;
                EstimatedTotalCost = estimatedTotalCost;
            }

            public WorldState State { get; }
            public float CostSoFar { get; }
            public float EstimatedTotalCost { get; }
        }

        private readonly struct PreviousStep
        {
            public PreviousStep(WorldState state, GoapAction action)
            {
                State = state;
                Action = action;
            }

            public WorldState State { get; }
            public GoapAction Action { get; }
        }

        public static GoapPlanResult Plan(
            WorldState start,
            WorldState goal,
            IReadOnlyList<GoapAction> availableActions,
            int maximumExpandedStates = 1024)
        {
            if (availableActions == null)
            {
                throw new ArgumentNullException(nameof(availableActions));
            }

            if (maximumExpandedStates <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumExpandedStates));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            float minimumActionCost = FindMinimumActionCost(availableActions);
            int maximumAddedFacts = FindMaximumAddedFacts(availableActions);

            var open = new List<OpenRecord>();
            var bestCosts = new Dictionary<WorldState, float>();
            var previous = new Dictionary<WorldState, PreviousStep>();

            bestCosts[start] = 0f;
            open.Add(new OpenRecord(
                start,
                0f,
                EstimateRemainingCost(
                    start,
                    goal,
                    minimumActionCost,
                    maximumAddedFacts)));

            int expandedStateCount = 0;

            while (open.Count > 0 &&
                   expandedStateCount < maximumExpandedStates)
            {
                int bestIndex = FindLowestCostIndex(open);
                OpenRecord current = open[bestIndex];
                open.RemoveAt(bestIndex);

                if (bestCosts.TryGetValue(
                        current.State,
                        out float currentBestCost) &&
                    current.CostSoFar > currentBestCost)
                {
                    continue;
                }

                if (current.State.Satisfies(goal))
                {
                    stopwatch.Stop();
                    return CreateSuccessResult(
                        start,
                        current.State,
                        previous,
                        current.CostSoFar,
                        expandedStateCount,
                        stopwatch.Elapsed.TotalMilliseconds);
                }

                expandedStateCount++;

                foreach (GoapAction action in availableActions)
                {
                    if (action == null ||
                        !action.IsApplicable(current.State))
                    {
                        continue;
                    }

                    float actionCost = action.GetCost(current.State);

                    if (actionCost <= 0f)
                    {
                        throw new InvalidOperationException(
                            $"Action '{action.Name}' returned a non-positive cost.");
                    }

                    WorldState nextState = action.Apply(current.State);
                    float proposedCost = current.CostSoFar + actionCost;

                    if (bestCosts.TryGetValue(
                            nextState,
                            out float knownCost) &&
                        proposedCost >= knownCost)
                    {
                        continue;
                    }

                    bestCosts[nextState] = proposedCost;
                    previous[nextState] = new PreviousStep(
                        current.State,
                        action);

                    float estimatedTotalCost = proposedCost +
                        EstimateRemainingCost(
                            nextState,
                            goal,
                            minimumActionCost,
                            maximumAddedFacts);

                    open.Add(new OpenRecord(
                        nextState,
                        proposedCost,
                        estimatedTotalCost));
                }
            }

            stopwatch.Stop();
            return new GoapPlanResult(
                false,
                Array.Empty<GoapAction>(),
                float.PositiveInfinity,
                expandedStateCount,
                stopwatch.Elapsed.TotalMilliseconds);
        }

        private static float FindMinimumActionCost(
            IReadOnlyList<GoapAction> actions)
        {
            float minimum = float.PositiveInfinity;

            foreach (GoapAction action in actions)
            {
                if (action != null)
                {
                    minimum = Math.Min(minimum, action.BaseCost);
                }
            }

            return float.IsPositiveInfinity(minimum) ? 0f : minimum;
        }

        private static int FindMaximumAddedFacts(
            IReadOnlyList<GoapAction> actions)
        {
            int maximum = 1;

            foreach (GoapAction action in actions)
            {
                if (action != null)
                {
                    maximum = Math.Max(
                        maximum,
                        action.EffectsToAdd.CountSetFacts());
                }
            }

            return maximum;
        }

        private static float EstimateRemainingCost(
            WorldState state,
            WorldState goal,
            float minimumActionCost,
            int maximumAddedFacts)
        {
            int missingFacts = state.CountMissing(goal);
            int minimumRemainingActions =
                (missingFacts + maximumAddedFacts - 1) /
                maximumAddedFacts;

            return minimumRemainingActions * minimumActionCost;
        }

        private static int FindLowestCostIndex(List<OpenRecord> open)
        {
            int bestIndex = 0;

            for (int index = 1; index < open.Count; index++)
            {
                if (open[index].EstimatedTotalCost <
                    open[bestIndex].EstimatedTotalCost)
                {
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        private static GoapPlanResult CreateSuccessResult(
            WorldState start,
            WorldState finalState,
            IReadOnlyDictionary<WorldState, PreviousStep> previous,
            float totalCost,
            int expandedStateCount,
            double elapsedMilliseconds)
        {
            var plan = new List<GoapAction>();
            WorldState current = finalState;

            while (current != start)
            {
                PreviousStep step = previous[current];
                plan.Add(step.Action);
                current = step.State;
            }

            plan.Reverse();

            return new GoapPlanResult(
                true,
                plan,
                totalCost,
                expandedStateCount,
                elapsedMilliseconds);
        }
    }
}
