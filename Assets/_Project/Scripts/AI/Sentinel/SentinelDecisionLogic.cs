using System.Collections.Generic;
using UnityEngine;

namespace BlackoutProtocol.AI.Sentinel
{
    /// <summary>Pure scoring and candidate generation used by Sentinel patrol and search.</summary>
    public static class SentinelDecisionLogic
    {
        public readonly struct PatrolOption
        {
            public PatrolOption(int index, float lastVisited, float routeLength, bool reachable)
            {
                Index = index;
                LastVisited = lastVisited;
                RouteLength = routeLength;
                Reachable = reachable;
            }

            public int Index { get; }
            public float LastVisited { get; }
            public float RouteLength { get; }
            public bool Reachable { get; }
        }

        public readonly struct SearchOption
        {
            public SearchOption(Vector3 position, float score) { Position = position; Score = score; }
            public Vector3 Position { get; }
            public float Score { get; }
        }

        public sealed class AlarmCooldown
        {
            private float nextAlarmTime;
            private bool hasAlarm;

            public bool CanRaise(float now) => !hasAlarm || now >= nextAlarmTime;

            public void Record(float now, float cooldown)
            {
                hasAlarm = true;
                nextAlarmTime = now + Mathf.Max(0f, cooldown);
            }
        }

        public static float ScorePatrol(float now, float lastVisited, float routeLength) =>
            (now - lastVisited) - routeLength * 0.2f;

        public static int SelectPatrol(IReadOnlyList<PatrolOption> options, int currentIndex, bool avoidCurrent)
        {
            if (options == null) return -1;
            int bestIndex = -1;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < options.Count; i++)
            {
                PatrolOption option = options[i];
                if (!option.Reachable || (avoidCurrent && option.Index == currentIndex)) continue;
                float score = -option.LastVisited - option.RouteLength * 0.2f;
                if (score > bestScore) { bestScore = score; bestIndex = option.Index; }
            }
            return bestIndex;
        }

        /// <summary>Returns five ordered search hypotheses: predicted heading, flanks, farther heading, then last seen.</summary>
        public static void BuildSearchOptions(Vector3 lastKnownPosition, Vector3 heading, float predictionSeconds,
            float searchRadius, List<SearchOption> results)
        {
            if (results == null) return;
            results.Clear();
            Vector3 forward = heading;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 predicted = lastKnownPosition + forward * predictionSeconds;
            results.Add(new SearchOption(predicted, 5f));
            results.Add(new SearchOption(predicted + right * searchRadius, 4f));
            results.Add(new SearchOption(predicted - right * searchRadius, 3f));
            results.Add(new SearchOption(predicted + forward * searchRadius, 2f));
            results.Add(new SearchOption(lastKnownPosition, 1f));
        }

        public static int FindNextReachableSearchIndex(IReadOnlyList<bool> reachable, int startIndex, int candidateCount)
        {
            if (reachable == null) return -1;
            int end = Mathf.Min(candidateCount, reachable.Count);
            for (int i = Mathf.Max(0, startIndex); i < end; i++)
            {
                if (reachable[i]) return i;
            }
            return -1;
        }
    }
}
