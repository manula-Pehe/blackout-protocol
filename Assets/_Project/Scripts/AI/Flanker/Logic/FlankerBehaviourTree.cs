using System;

namespace BlackoutProtocol.AI.Flanker.Logic
{
    /// <summary>Ordered tactical leaves of the Flanker's reactive tree.</summary>
    public enum FlankerTactic { None, Retreat, Punish, Hold, Flank, Investigate, Patrol }

    /// <summary>Behaviour tree node results.</summary>
    public enum BehaviourStatus { Failure, Running, Success }

    /// <summary>Perceived facts supplied by the Unity adapter, never queried by the tree.</summary>
    public struct FlankerFacts
    {
        public float OwnHealth;
        public float PlayerHealth;
        public bool PlayerVisible;
        public bool PlayerReloading;
        public bool AimedAt;
        public bool HasMemory;
    }

    /// <summary>Boundary between decision nodes and world actions.</summary>
    public interface IFlankerActions
    {
        BehaviourStatus Tick(FlankerTactic tactic);
        void Abort(FlankerTactic tactic);
    }

    /// <summary>Reactive priority selector of condition/action sequences, with explicit aborts.</summary>
    public sealed class FlankerBehaviourTree
    {
        private sealed class Sequence
        {
            public readonly FlankerTactic Tactic;
            public readonly Func<FlankerFacts, bool> Condition;

            public Sequence(FlankerTactic tactic, Func<FlankerFacts, bool> condition)
            {
                Tactic = tactic;
                Condition = condition;
            }
        }

        private readonly Sequence[] children =
        {
            new Sequence(FlankerTactic.Retreat, facts => facts.OwnHealth < 0.30f),
            new Sequence(FlankerTactic.Punish, facts => facts.PlayerVisible && (facts.PlayerReloading || facts.PlayerHealth < 0.35f)),
            new Sequence(FlankerTactic.Hold, facts => facts.PlayerVisible && facts.AimedAt),
            new Sequence(FlankerTactic.Flank, facts => facts.PlayerVisible),
            new Sequence(FlankerTactic.Investigate, facts => facts.HasMemory),
            new Sequence(FlankerTactic.Patrol, facts => true)
        };

        public FlankerTactic Active { get; private set; }

        /// <summary>Rechecks every higher priority each tick; aborts before starting a different action.</summary>
        public BehaviourStatus Tick(FlankerFacts facts, IFlankerActions actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (float.IsNaN(facts.OwnHealth) || facts.OwnHealth <= 0f)
            {
                Abort(actions);
                return BehaviourStatus.Failure;
            }
            foreach (Sequence child in children)
            {
                if (!child.Condition(facts)) continue;
                if (Active != child.Tactic)
                {
                    Abort(actions);
                    Active = child.Tactic;
                }
                BehaviourStatus result = actions.Tick(child.Tactic);
                if (result != BehaviourStatus.Failure) return result;
                Abort(actions);
            }
            Abort(actions);
            return BehaviourStatus.Failure;
        }

        /// <summary>Releases the active action on disable, target replacement or death.</summary>
        public void Abort(IFlankerActions actions)
        {
            if (Active != FlankerTactic.None) actions.Abort(Active);
            Active = FlankerTactic.None;
        }
    }
}
