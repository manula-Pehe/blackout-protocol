using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BlackoutProtocol.AI.Sentinel
{
    /// <summary>On-demand runtime diagnostics for the Sentinel's state, choices and path searches.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SentinelDroneBrain))]
    public sealed class SentinelDebugView : MonoBehaviour
    {
        [SerializeField] private SentinelDroneBrain brain;
        [SerializeField] private bool visibleAtStart;
        [SerializeField, Min(12)] private int panelWidth = 430;

        private readonly StringBuilder text = new StringBuilder(1024);
        private Vector2 scroll;
        private GUIStyle bodyStyle;
        private GUIStyle headerStyle;
        private bool visible;
        private string comparisonMessage = "Select a graph-based route, then compare A* with UCS.";

        private void Awake()
        {
            if (brain == null) brain = GetComponent<SentinelDroneBrain>();
            visible = visibleAtStart;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) visible = !visible;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1)) visible = !visible;
#endif
        }

        private void OnGUI()
        {
            if (brain == null || !visible) return;

            if (bodyStyle == null)
            {
                bodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true, fontSize = 13 };
                headerStyle = new GUIStyle(bodyStyle) { fontSize = 16, fontStyle = FontStyle.Bold };
            }

            float width = Mathf.Min(panelWidth, Screen.width - 24f);
            float height = Mathf.Min(560f, Screen.height - 24f);
            GUILayout.BeginArea(new Rect(12, 12, width, height), GUI.skin.window);
            GUILayout.BeginHorizontal();
            GUILayout.Label("SENTINEL / DIAGNOSTICS", headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("×", GUILayout.Width(26))) visible = false;
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            DrawStateAndPerception();
            DrawCandidates();
            DrawPathComparison();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawStateAndPerception()
        {
            GUILayout.Label("HFSM  " + brain.StatePath, bodyStyle);
            GUILayout.Label("VISION  " + (brain.PlayerVisible ? "PLAYER VISIBLE" : "no player in sight") +
                $"  /  {brain.FieldOfView:0}°  /  {brain.SightRange:0.0} m", bodyStyle);
            GUILayout.Label(brain.HasLastKnownPosition
                ? "LAST KNOWN  " + brain.LastKnownPosition.ToString("F1")
                : "LAST KNOWN  none", bodyStyle);
            GUILayout.Label(brain.HasDestination
                ? "DESTINATION  " + brain.CurrentDestination.ToString("F1")
                : "DESTINATION  none", bodyStyle);
            if (brain.State == SentinelState.Search)
                GUILayout.Label($"SEARCH CANDIDATE  {brain.SearchCandidateIndex + 1}", bodyStyle);
        }

        private void DrawCandidates()
        {
            GUILayout.Space(5);
            GUILayout.Label("DECISION CANDIDATES", headerStyle);
            var candidates = brain.DebugCandidates;
            if (candidates.Count == 0)
            {
                GUILayout.Label("No destination scoring has run yet.", bodyStyle);
                return;
            }

            text.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                SentinelDebugCandidate candidate = candidates[i];
                text.Append(candidate.Selected ? "> " : "  ").Append(candidate.Label).Append("  ");
                if (!candidate.Reachable) text.Append("unreachable");
                else text.Append("score ").Append(candidate.Score.ToString("0.00"));
                if (candidate.Selected) text.Append("  [SELECTED]");
                text.Append('\n');
            }
            GUILayout.Label(text.ToString(), bodyStyle);
        }

        private void DrawPathComparison()
        {
            GUILayout.Space(5);
            GUILayout.Label("A* vs UCS  (same NavMesh graph and endpoints; UCS uses h = 0)", headerStyle);
            SentinelNavMeshPathfinder pathfinder = brain.Pathfinder;
            if (pathfinder == null)
            {
                GUILayout.Label("Pathfinder is not initialized yet.", bodyStyle);
                return;
            }

            GUILayout.BeginHorizontal();
            bool oldEnabled = GUI.enabled;
            GUI.enabled = pathfinder.CanCompareLastSearch;
            if (GUILayout.Button("Repeat latest graph query"))
            {
                bool succeeded = pathfinder.TryCompareLastSearch(out _, out _);
                comparisonMessage = succeeded ? "Both searches found a route." : pathfinder.LastQueryStatus;
            }
            GUI.enabled = brain.HasDestination;
            if (GUILayout.Button("Compare current destination"))
            {
                bool succeeded = pathfinder.TryCompareSearches(brain.transform.position, brain.CurrentDestination, out _, out _);
                comparisonMessage = succeeded ? "Both searches found a route on graph anchors." : pathfinder.LastQueryStatus;
            }
            GUI.enabled = oldEnabled;
            GUILayout.EndHorizontal();

            GUILayout.Label(pathfinder.LastQueryStatus, bodyStyle);
            SentinelPathSearchStats aStar = pathfinder.LastAStar;
            GUILayout.Label(FormatStats("A*", aStar), bodyStyle);
            if (pathfinder.LastUniformCost.Succeeded || pathfinder.LastUniformCost.NodesExpanded > 0)
            {
                SentinelPathSearchStats ucs = pathfinder.LastUniformCost;
                GUILayout.Label(FormatStats("UCS", ucs), bodyStyle);
                GUILayout.Label($"Expanded-node reduction: {Reduction(aStar.NodesExpanded, ucs.NodesExpanded):0.0}%  /  cost delta: {Mathf.Abs(aStar.TotalCost - ucs.TotalCost):0.0000}", bodyStyle);
            }
            GUILayout.Label(comparisonMessage, bodyStyle);
            GUILayout.Label("Elapsed values are search-only timings; compare repeated runs and treat short timings as noisy.", bodyStyle);
        }

        private static string FormatStats(string name, SentinelPathSearchStats stats) =>
            stats.Succeeded
                ? $"{name}  cost {stats.TotalCost:0.000}  /  expanded {stats.NodesExpanded}  /  {stats.ElapsedMilliseconds:0.000} ms"
                : $"{name}  no route  /  expanded {stats.NodesExpanded}  /  {stats.ElapsedMilliseconds:0.000} ms";

        private static float Reduction(int aStarExpanded, int ucsExpanded) =>
            ucsExpanded <= 0 ? 0f : (ucsExpanded - aStarExpanded) * 100f / ucsExpanded;

        private void OnDrawGizmosSelected()
        {
            SentinelNavMeshPathfinder pathfinder = brain != null ? brain.Pathfinder : null;
            if (pathfinder == null) return;
            DrawPath(pathfinder.LastAStarPath, new Color(0.1f, 0.9f, 1f, 0.9f));
            DrawPath(pathfinder.LastUniformCostPath, new Color(1f, 0.55f, 0.1f, 0.9f));
        }

        private static void DrawPath(System.Collections.Generic.IReadOnlyList<Vector3> path, Color color)
        {
            if (path == null || path.Count < 2) return;
            Gizmos.color = color;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 from = path[i - 1] + Vector3.up * 0.12f;
                Vector3 to = path[i] + Vector3.up * 0.12f;
                Gizmos.DrawLine(from, to);
                Gizmos.DrawSphere(to, 0.07f);
            }
        }
    }
}
