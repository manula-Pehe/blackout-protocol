using System;

namespace BlackoutProtocol.AI.Breacher.Logic
{
    public enum ActionExecutionStatus
    {
        Running,
        Succeeded,
        Failed
    }

    public enum PlanExecutionStatus
    {
        Idle,
        Running,
        Completed,
        NeedsReplan
    }

    public enum ReplanReason
    {
        None,
        InvalidPlan,
        PreconditionsFailed,
        ActionFailed,
        LevelChanged,
        AgentStuck,
        PlayerMoved,
        GoalChanged
    }

    /// <summary>
    /// Bridges pure GOAP actions to their eventual Unity-world execution.
    /// </summary>
    public interface IGoapActionRunner
    {
        void Begin(GoapAction action);
        ActionExecutionStatus Tick(GoapAction action);
        void Cancel(GoapAction action);
    }

    /// <summary>
    /// Executes a GOAP plan sequentially and reports when replanning is needed.
    /// </summary>
    public sealed class PlanExecutor
    {
        private readonly IGoapActionRunner actionRunner;

        private GoapPlanResult plan;
        private WorldState currentWorldState;
        private int currentActionIndex;

        public PlanExecutor(IGoapActionRunner actionRunner)
        {
            this.actionRunner = actionRunner ??
                throw new ArgumentNullException(nameof(actionRunner));
        }

        public PlanExecutionStatus Status { get; private set; } =
            PlanExecutionStatus.Idle;

        public ReplanReason ReplanReason { get; private set; } =
            ReplanReason.None;

        public int CurrentActionIndex => currentActionIndex;
        public WorldState CurrentWorldState => currentWorldState;

        public GoapAction CurrentAction
        {
            get
            {
                if (plan == null ||
                    currentActionIndex < 0 ||
                    currentActionIndex >= plan.Actions.Count)
                {
                    return null;
                }

                return plan.Actions[currentActionIndex];
            }
        }

        public void StartPlan(
            GoapPlanResult newPlan,
            WorldState initialWorldState)
        {
            CancelCurrentActionIfNeeded();

            plan = newPlan;
            currentWorldState = initialWorldState;
            currentActionIndex = 0;
            ReplanReason = ReplanReason.None;

            if (plan == null || !plan.Succeeded)
            {
                Status = PlanExecutionStatus.NeedsReplan;
                ReplanReason = ReplanReason.InvalidPlan;
                return;
            }

            if (plan.Actions.Count == 0)
            {
                Status = PlanExecutionStatus.Completed;
                return;
            }

            Status = PlanExecutionStatus.Running;
            BeginCurrentAction();
        }

        public void Tick()
        {
            if (Status != PlanExecutionStatus.Running)
            {
                return;
            }

            GoapAction action = CurrentAction;

            if (action == null)
            {
                RequestReplan(ReplanReason.InvalidPlan);
                return;
            }

            if (!action.IsApplicable(currentWorldState))
            {
                RequestReplan(ReplanReason.PreconditionsFailed);
                return;
            }

            ActionExecutionStatus actionStatus =
                actionRunner.Tick(action);

            switch (actionStatus)
            {
                case ActionExecutionStatus.Running:
                    return;

                case ActionExecutionStatus.Failed:
                    RequestReplan(ReplanReason.ActionFailed);
                    return;

                case ActionExecutionStatus.Succeeded:
                    CompleteCurrentAction(action);
                    return;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void UpdateWorldState(WorldState sensedWorldState)
        {
            currentWorldState = sensedWorldState;

            if (Status == PlanExecutionStatus.Running &&
                CurrentAction != null &&
                !CurrentAction.IsApplicable(currentWorldState))
            {
                RequestReplan(ReplanReason.PreconditionsFailed);
            }
        }

        public void NotifyLevelChanged()
        {
            RequestReplanIfRunning(ReplanReason.LevelChanged);
        }

        public void NotifyAgentStuck()
        {
            RequestReplanIfRunning(ReplanReason.AgentStuck);
        }

        public void NotifyPlayerMoved()
        {
            RequestReplanIfRunning(ReplanReason.PlayerMoved);
        }

        public void NotifyGoalChanged()
        {
            RequestReplanIfRunning(ReplanReason.GoalChanged);
        }

        private void BeginCurrentAction()
        {
            GoapAction action = CurrentAction;

            if (action == null)
            {
                RequestReplan(ReplanReason.InvalidPlan);
                return;
            }

            if (!action.IsApplicable(currentWorldState))
            {
                RequestReplan(ReplanReason.PreconditionsFailed);
                return;
            }

            actionRunner.Begin(action);
        }

        private void CompleteCurrentAction(GoapAction action)
        {
            currentWorldState = action.Apply(currentWorldState);
            currentActionIndex++;

            if (currentActionIndex >= plan.Actions.Count)
            {
                Status = PlanExecutionStatus.Completed;
                return;
            }

            BeginCurrentAction();
        }

        private void RequestReplanIfRunning(ReplanReason reason)
        {
            if (Status == PlanExecutionStatus.Running)
            {
                RequestReplan(reason);
            }
        }

        private void RequestReplan(ReplanReason reason)
        {
            CancelCurrentActionIfNeeded();
            Status = PlanExecutionStatus.NeedsReplan;
            ReplanReason = reason;
        }

        private void CancelCurrentActionIfNeeded()
        {
            if (Status == PlanExecutionStatus.Running &&
                CurrentAction != null)
            {
                actionRunner.Cancel(CurrentAction);
            }
        }
    }
}
