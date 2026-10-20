using System;

namespace BlackoutProtocol.AI.Hunter.Logic
{
    public enum HunterDecisionReason
    {
        UtilityScores,
        TrustedSound,
        VisiblePlayer,
        TargetLostOrInvalid,
        TrailCooling,
        MemoryExpired,
        ArrivedAtTrail,
        UnreachablePursuit,
        RepeatedStuck,
        SearchCompleted
    }

    /// <summary>The actual selection and triggering event at a runtime action change.</summary>
    public readonly struct HunterActionTransition
    {
        public HunterActionTransition(double time, HunterAction from, HunterUtilitySelection selection, HunterDecisionReason reason)
        {
            Time = time;
            From = from;
            Selection = selection;
            Reason = reason;
        }

        public double Time { get; }
        public HunterAction From { get; }
        public HunterAction To => Selection.Action;
        public HunterUtilitySelection Selection { get; }
        public HunterDecisionReason Reason { get; }
    }

    /// <summary>Fixed-capacity transition ring. Recording and reading do not allocate.</summary>
    public sealed class HunterTransitionHistory
    {
        private readonly HunterActionTransition[] entries;
        private int next;
        public HunterTransitionHistory(int capacity = 12) => entries = new HunterActionTransition[Math.Max(1, Math.Min(32, capacity))];
        public int Count { get; private set; }
        public int Capacity => entries.Length;

        public void Record(HunterActionTransition entry)
        {
            if (entry.From == entry.To) return;
            entries[next] = entry;
            next = (next + 1) % entries.Length;
            Count = Math.Min(Count + 1, entries.Length);
        }

        public HunterActionTransition GetRecent(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return entries[(next - 1 - index + entries.Length) % entries.Length];
        }
    }

    public readonly struct HunterDebugScoreRow
    {
        public HunterDebugScoreRow(HunterAction action, HunterUtilitySelection selection)
        {
            Action = action;
            BaseScore = selection.BaseScores.Get(action);
            AdjustedScore = selection.Scores.Get(action);
            Selected = selection.Action == action;
        }

        public HunterAction Action { get; }
        public float BaseScore { get; }
        public float AdjustedScore { get; }
        public bool Selected { get; }
    }
}
