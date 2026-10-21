using System.Text;
using BlackoutProtocol.AI.Hunter.Logic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BlackoutProtocol.AI.Hunter
{
    /// <summary>Read-only decision display. Only explicit navigation comparisons can request extra search work.</summary>
    [DisallowMultipleComponent]
    public sealed class HunterDebugPanel : MonoBehaviour
    {
        [SerializeField] private HunterBrain brain;
        [SerializeField] private HunterNavMeshPathfinder pathfinder;
        [SerializeField] private bool overlayVisible = true;
        [SerializeField] private bool compareOnRouteRequests;
        [Tooltip("Presentation only: responsive narrow panel for the dedicated technical demo. Existing sandboxes keep their original layout.")]
        [SerializeField] private bool compactDemoLayout;
        private readonly StringBuilder text = new StringBuilder(2048);
        private float nextRefresh;
        private Vector2 scroll;
        private GUIStyle labelStyle;
        private string header = "HUNTER / waiting for decision / F1 debug";

        public bool OverlayVisible => overlayVisible;
        public bool CompareOnRouteRequests => compareOnRouteRequests;
        public HunterUtilitySelection DisplayedSelection { get; private set; }
        public double DisplayedDecisionTime { get; private set; }
        public HunterRememberedNoise DisplayedNoise { get; private set; }
        public bool HasDisplayedNoise { get; private set; }
        public double DisplayedNoiseAge { get; private set; }
        public string ScoreText { get; private set; } = "Waiting for a scheduled decision.";
        public string MemoryText { get; private set; } = "Memory: none";
        public string NavigationText { get; private set; } = "Navigation: no route requested.";
        public string ComparisonText { get; private set; } = "UCS: not requested.";
        public string HistoryText { get; private set; } = "No action transitions yet.";

        private void Awake() => ResolveReferences();

        private void ResolveReferences()
        {
            if (brain == null) brain = GetComponent<HunterBrain>();
            if (pathfinder == null) pathfinder = GetComponent<HunterNavMeshPathfinder>();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyComparisonMode();
            nextRefresh = 0;
        }

        private void OnDisable()
        {
            if (pathfinder != null) pathfinder.CompareOnRouteRequest = false;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) ToggleOverlay();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1)) ToggleOverlay();
#endif
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            if (overlayVisible) RefreshDisplay();
            else if (brain != null) header = $"HUNTER / {brain.CurrentAction} / F1 debug";
        }

        public void ToggleOverlay()
        {
            overlayVisible = !overlayVisible;
            ApplyComparisonMode();
            nextRefresh = 0;
        }

        public void SetCompareOnRouteRequests(bool value)
        {
            compareOnRouteRequests = value;
            ApplyComparisonMode();
        }

        private void ApplyComparisonMode()
        {
            if (pathfinder != null)
                pathfinder.CompareOnRouteRequest = isActiveAndEnabled && overlayVisible && compareOnRouteRequests;
        }

        public bool CompareLatestRoute()
        {
            bool succeeded = pathfinder != null && pathfinder.TryCompareLastSearch(out _, out _);
            RefreshDisplay();
            return succeeded;
        }

        public HunterDebugScoreRow GetDisplayedScoreRow(HunterAction action) => new HunterDebugScoreRow(action, DisplayedSelection);

        /// <summary>Copies existing runtime facts only: no selector, perception, memory query or path search.</summary>
        public void RefreshDisplay()
        {
            if (brain == null || pathfinder == null)
            {
                ScoreText = "Assign HunterBrain and HunterNavMeshPathfinder on this debug panel.";
                return;
            }
            header = $"HUNTER / {brain.CurrentAction} / F1 debug";
            DisplayedSelection = brain.LastSelection;
            DisplayedDecisionTime = brain.LastDecisionTime;
            HasDisplayedNoise = brain.HasSelectedNoise;
            DisplayedNoise = HasDisplayedNoise ? brain.SelectedNoise : default;
            DisplayedNoiseAge = HasDisplayedNoise ? System.Math.Max(0, DisplayedDecisionTime - DisplayedNoise.Observation.Time) : 0;

            text.Clear();
            text.Append("UTILITY / actual decision at ").Append(DisplayedDecisionTime.ToString("F2")).Append(" s\n");
            for (HunterAction action = HunterAction.Wander; action <= HunterAction.Attack; action++)
            {
                HunterDebugScoreRow row = GetDisplayedScoreRow(action);
                if (row.Selected) text.Append("<b>> ");
                text.Append(action).Append("   base ").Append(row.BaseScore.ToString("F3"))
                    .Append("   adjusted ").Append(row.AdjustedScore.ToString("F3"));
                if (row.Selected) text.Append("  [SELECTED]</b>");
                text.Append('\n');
            }
            ScoreText = text.ToString();

            text.Clear();
            if (!HasDisplayedNoise) text.Append("MEMORY / none (expired or empty)");
            else
            {
                text.Append("MEMORY / ").Append(DisplayedNoise.Observation.Category)
                    .Append(" / age ").Append(DisplayedNoiseAge.ToString("F2")).Append(" s at decision\nconfidence ")
                    .Append(DisplayedNoise.Confidence.ToString("F3")).Append(" / trust ")
                    .Append(DisplayedNoise.EffectiveTrust.ToString("F3")).Append(" / priority ")
                    .Append(DisplayedNoise.Priority.ToString("F3"));
            }
            MemoryText = text.ToString();

            text.Clear();
            text.Append("NAVIGATION / ").Append(brain.HasActiveRoute ? "following" : "stopped or waiting")
                .Append("\ndestination ").Append(brain.HasDestination ? brain.Destination.ToString("F2") : "none")
                .Append(" / waypoints ").Append(brain.Waypoints.Count)
                .Append("\nrequests ").Append(brain.PathRequestCount).Append(" / failures ").Append(brain.PathFailureCount)
                .Append(" / stuck recoveries ").Append(brain.StuckRecoveryCount)
                .Append("\n").Append(pathfinder.NavigationStatus).Append("\n").Append(brain.Status)
                .Append("\nEvent: ").Append(brain.LastRuntimeEvent);
            NavigationText = text.ToString();

            text.Clear();
            text.Append("A* vs UCS / query ").Append(pathfinder.QueryVersion).Append(" / anchors ")
                .Append(pathfinder.StartAnchor).Append(" -> ").Append(pathfinder.GoalAnchor).Append('\n');
            AppendSearch("A*", pathfinder.LastSearch);
            AppendSearch("UCS", pathfinder.LastUniformCostSearch);
            if (pathfinder.LastSearch != null && pathfinder.LastSearch.Succeeded &&
                pathfinder.LastUniformCostSearch != null && pathfinder.LastUniformCostSearch.Succeeded)
            {
                double delta = System.Math.Abs(pathfinder.LastSearch.TotalCost - pathfinder.LastUniformCostSearch.TotalCost);
                text.Append("Cost difference: ").Append(delta.ToString("F6")).Append('\n');
            }
            text.Append("Costs exclude endpoint legs. Times measure search only.\nGraph builds ")
                .Append(pathfinder.GraphBuildCount).Append(" / A* runs ").Append(pathfinder.AStarSearchCount)
                .Append(" / UCS runs ").Append(pathfinder.UniformCostSearchCount);
            ComparisonText = text.ToString();

            text.Clear();
            text.Append("RECENT ACTION CHANGES / newest first\n");
            HunterTransitionHistory history = brain.Transitions;
            if (history.Count == 0) text.Append("No transitions yet.\n");
            for (int i = 0; i < history.Count; i++)
            {
                HunterActionTransition entry = history.GetRecent(i);
                text.Append(entry.Time.ToString("F2")).Append(" s / ").Append(entry.From).Append(" -> ").Append(entry.To)
                    .Append(" / ").Append(Explain(entry.Reason)).Append('\n')
                    .Append("  adjusted ").Append(entry.Selection.Scores.Get(entry.From).ToString("F3"))
                    .Append(" -> ").Append(entry.Selection.Score.ToString("F3"))
                    .Append(" (winner base ").Append(entry.Selection.BaseScores.Get(entry.To).ToString("F3")).Append(")\n");
            }
            HistoryText = text.ToString();
        }

        private void AppendSearch(string name, HunterPathResult result)
        {
            text.Append(name).Append(": ");
            if (result == null) { text.Append("not run for this query\n"); return; }
            if (result.Succeeded) text.Append("cost ").Append(result.TotalCost.ToString("F3"));
            else text.Append("FAILED");
            text.Append(" / expanded ").Append(result.NodesExpanded)
                .Append(" / ").Append(result.ElapsedMilliseconds.ToString("F3")).Append(" ms\n");
        }

        private static string Explain(HunterDecisionReason reason)
        {
            switch (reason)
            {
                case HunterDecisionReason.TrustedSound: return "trusted unresolved sound";
                case HunterDecisionReason.VisiblePlayer: return "live visible player";
                case HunterDecisionReason.TargetLostOrInvalid: return "target lost / invalid / pursuit cooldown";
                case HunterDecisionReason.TrailCooling: return "trail confidence or utility cooled";
                case HunterDecisionReason.MemoryExpired: return "sound memory expired";
                case HunterDecisionReason.ArrivedAtTrail: return "AgentBody arrived at trail";
                case HunterDecisionReason.UnreachablePursuit: return "A* pursuit failed; local search";
                case HunterDecisionReason.RepeatedStuck: return "repeated stuck; pursuit cooldown";
                case HunterDecisionReason.SearchCompleted: return "search duration completed";
                default: return "utility scores / hysteresis";
            }
        }

        private void OnGUI()
        {
            Matrix4x4 previous = GUI.matrix;
            float scale = compactDemoLayout ? Mathf.Max(0.1f, Mathf.Min(Screen.width / 1280f, Screen.height / 800f)) : 1;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            try
            {
                if (compactDemoLayout)
                {
                    // The demo camera only renders its own viewport. Clear this
                    // reserved UI region, including when F1 collapses the panel.
                    Color previousColor = GUI.color;
                    GUI.color = new Color(0.025f, 0.04f, 0.065f, 1);
                    GUI.DrawTexture(new Rect(0, 0, 416, Screen.height / scale - 174), Texture2D.whiteTexture);
                    GUI.color = previousColor;
                }
                DrawPanel(Screen.width / scale, Screen.height / scale);
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawPanel(float width, float height)
        {
            if (!overlayVisible)
            {
                GUI.Box(new Rect(16, 16, Mathf.Min(compactDemoLayout ? 390 : 650, width - 32), 32), header);
                return;
            }
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            if (compactDemoLayout) labelStyle.fontSize = 14;
            Rect area = new Rect(16, 16, Mathf.Min(compactDemoLayout ? 390 : 540, width - 32), Mathf.Max(40, Mathf.Min(650, height - 200)));
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(header, labelStyle);
            GUILayout.BeginHorizontal();
            bool oldEnabled = GUI.enabled;
            GUI.enabled = pathfinder != null && pathfinder.CanCompareLastSearch;
            if (GUILayout.Button(compactDemoLayout ? "Compare A* / UCS" : "Compare latest route")) CompareLatestRoute();
            GUI.enabled = oldEnabled;
            bool automatic = GUILayout.Toggle(compareOnRouteRequests, compactDemoLayout ? "Auto UCS" : "UCS on route requests");
            if (automatic != compareOnRouteRequests) SetCompareOnRouteRequests(automatic);
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(ScoreText, labelStyle);
            GUILayout.Label(MemoryText, labelStyle);
            GUILayout.Label(NavigationText, labelStyle);
            GUILayout.Label(ComparisonText, labelStyle);
            GUILayout.Label(HistoryText, labelStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
