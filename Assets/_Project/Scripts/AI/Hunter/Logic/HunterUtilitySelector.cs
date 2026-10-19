using System;

namespace BlackoutProtocol.AI.Hunter.Logic
{
    /// <summary>Immutable utility values for every Hunter action.</summary>
    public readonly struct HunterUtilityScores
    {
        public HunterUtilityScores(
            float wander,
            float investigate,
            float chase,
            float search,
            float attack)
        {
            Wander = wander;
            Investigate = investigate;
            Chase = chase;
            Search = search;
            Attack = attack;
        }

        public float Wander { get; }
        public float Investigate { get; }
        public float Chase { get; }
        public float Search { get; }
        public float Attack { get; }

        /// <summary>Gets an action's score, or zero for an invalid enum value.</summary>
        public float Get(HunterAction action)
        {
            switch (action)
            {
                case HunterAction.Wander: return Wander;
                case HunterAction.Investigate: return Investigate;
                case HunterAction.Chase: return Chase;
                case HunterAction.Search: return Search;
                case HunterAction.Attack: return Attack;
                default: return 0f;
            }
        }

        internal HunterUtilityScores Add(HunterAction action, float amount)
        {
            return new HunterUtilityScores(
                Wander + (action == HunterAction.Wander ? amount : 0f),
                Investigate + (action == HunterAction.Investigate ? amount : 0f),
                Chase + (action == HunterAction.Chase ? amount : 0f),
                Search + (action == HunterAction.Search ? amount : 0f),
                Attack + (action == HunterAction.Attack ? amount : 0f));
        }
    }

    /// <summary>One utility decision plus its base and hysteresis-adjusted scores.</summary>
    public readonly struct HunterUtilitySelection
    {
        public HunterUtilitySelection(
            HunterAction action,
            HunterUtilityScores baseScores,
            HunterUtilityScores scores)
        {
            Action = action;
            BaseScores = baseScores;
            Scores = scores;
            Score = scores.Get(action);
        }

        public HunterAction Action { get; }
        public float Score { get; }
        public HunterUtilityScores BaseScores { get; }
        public HunterUtilityScores Scores { get; }
    }

    /// <summary>
    /// Scores Hunter behaviours with small, explicit response curves. The class has no
    /// engine or scene dependencies, allowing the same inputs to produce the same result.
    /// </summary>
    public sealed class HunterUtilitySelector
    {
        public const float DefaultHysteresisBonus = 0.08f;

        public HunterUtilitySelector(float hysteresisBonus = DefaultHysteresisBonus)
        {
            HysteresisBonus = Normalize(hysteresisBonus);
        }

        /// <summary>
        /// Bonus added to the current action before comparison. Values are clamped to 0..1.
        /// </summary>
        public float HysteresisBonus { get; }

        /// <summary>Selects without a current action, so no hysteresis is applied.</summary>
        public HunterUtilitySelection Select(HunterUtilityContext context)
        {
            HunterUtilityScores scores = CalculateScores(context);
            HunterAction action = SelectWinner(scores);
            return new HunterUtilitySelection(action, scores, scores);
        }

        /// <summary>
        /// Selects while favouring the current action by <see cref="HysteresisBonus"/>.
        /// Invalid enum values are ignored safely.
        /// </summary>
        public HunterUtilitySelection Select(
            HunterUtilityContext context,
            HunterAction currentAction)
        {
            HunterUtilityScores baseScores = CalculateScores(context);
            HunterUtilityScores adjustedScores = IsKnownAction(currentAction)
                ? baseScores.Add(currentAction, HysteresisBonus)
                : baseScores;
            HunterAction action = SelectWinner(adjustedScores);
            return new HunterUtilitySelection(action, baseScores, adjustedScores);
        }

        /// <summary>Calculates the five base scores without applying hysteresis.</summary>
        public HunterUtilityScores CalculateScores(HunterUtilityContext context)
        {
            // SmoothStep is an S-curve: weak signals stay calm, useful mid-range
            // changes are noticeable, and near-maximum inputs level off smoothly.
            float sound = SmoothStep(context.SoundIntensity);
            float memory = SmoothStep(context.MemoryConfidence);
            float proximity = SmoothStep(context.TargetProximity);
            float urgency = SmoothStep(context.SearchUrgency);
            float trust = context.NoiseTrust;
            float attackEligibility = context.AttackEligibility;

            // Wander has a modest idle baseline and fades as any purposeful cue rises.
            float strongestCue = Max(
                sound,
                memory,
                urgency,
                attackEligibility);
            float wander = 0.2f + 0.3f * (1f - strongestCue);

            // Investigation follows trusted sound, with a small preference for distant
            // sounds because close, well-known targets are better handled by Chase.
            float investigate = sound * trust * (1f - 0.35f * proximity);

            // Chase blends live sound with remembered confidence and rises strongly as
            // the Hunter closes on the target. Untrusted sounds cannot cause a chase.
            float chase =
                (0.55f * sound + 0.45f * memory) *
                trust *
                (0.3f + 0.7f * proximity);

            // Search is driven by urgency and remembered confidence, but fresh sound
            // suppresses it because that sound gives Investigate/Chase a clearer goal.
            float search =
                urgency *
                (0.3f + 0.7f * memory) *
                (1f - 0.6f * sound);

            // Eligibility is a hard gate. Proximity shapes urgency once attacking is valid.
            float attack = attackEligibility * (0.35f + 0.65f * proximity);

            return new HunterUtilityScores(
                Normalize(wander),
                Normalize(investigate),
                Normalize(chase),
                Normalize(search),
                Normalize(attack));
        }

        private static HunterAction SelectWinner(HunterUtilityScores scores)
        {
            // Fixed threat priority provides deterministic ties:
            // Attack > Chase > Investigate > Search > Wander.
            HunterAction winner = HunterAction.Attack;
            float winningScore = scores.Attack;

            Consider(HunterAction.Chase, scores.Chase, ref winner, ref winningScore);
            Consider(HunterAction.Investigate, scores.Investigate, ref winner, ref winningScore);
            Consider(HunterAction.Search, scores.Search, ref winner, ref winningScore);
            Consider(HunterAction.Wander, scores.Wander, ref winner, ref winningScore);
            return winner;
        }

        private static void Consider(
            HunterAction candidate,
            float score,
            ref HunterAction winner,
            ref float winningScore)
        {
            // Strict comparison preserves the earlier action on an exact tie.
            if (score > winningScore)
            {
                winner = candidate;
                winningScore = score;
            }
        }

        private static bool IsKnownAction(HunterAction action)
        {
            return action >= HunterAction.Wander && action <= HunterAction.Attack;
        }

        private static float SmoothStep(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float Max(float a, float b, float c, float d)
        {
            return Math.Max(Math.Max(a, b), Math.Max(c, d));
        }

        private static float Normalize(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
            {
                return 0f;
            }

            if (float.IsPositiveInfinity(value) || value >= 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}
