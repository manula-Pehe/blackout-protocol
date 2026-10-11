using System;

namespace BlackoutProtocol.AI.Breacher.Logic
{
    /// <summary>
    /// Planner-facing description of one action the Breacher may perform.
    /// Runtime actions can override applicability and cost later.
    /// </summary>
    public abstract class GoapAction
    {
        protected GoapAction(
            string name,
            WorldState preconditions,
            WorldState effectsToAdd,
            WorldState effectsToRemove,
            float baseCost)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "An action requires a name.",
                    nameof(name));
            }

            if (baseCost <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseCost),
                    "An action cost must be greater than zero.");
            }

            Name = name;
            Preconditions = preconditions;
            EffectsToAdd = effectsToAdd;
            EffectsToRemove = effectsToRemove;
            BaseCost = baseCost;
        }

        public string Name { get; }
        public WorldState Preconditions { get; }
        public WorldState EffectsToAdd { get; }
        public WorldState EffectsToRemove { get; }
        public float BaseCost { get; }

        public virtual bool IsApplicable(WorldState state)
        {
            return state.Satisfies(Preconditions);
        }

        public virtual float GetCost(WorldState state)
        {
            return BaseCost;
        }

        public WorldState Apply(WorldState state)
        {
            return state
                .WithoutAll(EffectsToRemove)
                .WithAll(EffectsToAdd);
        }
    }

    /// <summary>
    /// A data-only action used by the planner and its unit tests.
    /// </summary>
    public sealed class GoapActionDefinition : GoapAction
    {
        public GoapActionDefinition(
            string name,
            WorldState preconditions,
            WorldState effectsToAdd,
            WorldState effectsToRemove,
            float baseCost)
            : base(
                name,
                preconditions,
                effectsToAdd,
                effectsToRemove,
                baseCost)
        {
        }
    }
}
