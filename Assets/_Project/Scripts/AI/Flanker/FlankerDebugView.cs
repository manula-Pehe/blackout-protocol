using UnityEngine;

namespace BlackoutProtocol.AI.Flanker
{
    /// <summary>Shows the Flanker's current tactic and an on-demand A* versus UCS comparison.</summary>
    [DisallowMultipleComponent]
    public sealed class FlankerDebugView : MonoBehaviour
    {
        private FlankerBrain brain;
        private FlankerNavMeshPathfinder navigation;
        private bool visible;

        private void Awake()
        {
            brain = GetComponent<FlankerBrain>();
            navigation = GetComponent<FlankerNavMeshPathfinder>();
        }

        private void OnGUI()
        {
            if (!visible) return;
            Rect area = new Rect(Screen.width - 332f, 55f, 318f, 155f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("BLUE FLANKER  |  F2 closes");
            GUILayout.Label("Tactic: " + (brain != null ? brain.CurrentTactic.ToString() : "Unavailable"));
            if (navigation != null)
            {
                GUILayout.Label($"A*: cost {navigation.LastCost:F2}, expanded {navigation.LastAStarExpanded}, {navigation.LastAStarMilliseconds:F3} ms");
                if (GUILayout.Button("Compare latest route with UCS")) navigation.CompareLastRoute();
                if (navigation.HasComparison)
                    GUILayout.Label($"UCS: cost {navigation.LastUcsCost:F2}, expanded {navigation.LastUcsExpanded}, {navigation.LastUcsMilliseconds:F3} ms");
            }
            GUILayout.EndArea();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f2Key.wasPressedThisFrame) visible = !visible;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F2)) visible = !visible;
#endif
        }
    }
}
