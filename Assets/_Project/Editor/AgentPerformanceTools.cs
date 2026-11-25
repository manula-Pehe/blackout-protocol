using BlackoutProtocol.Body.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AgentPerformanceTools
{
    [MenuItem("Blackout Protocol/Performance/Run Main 4 vs 12 Agent Benchmark")]
    private static void Run()
    {
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Main")
        { Debug.LogWarning("Open Main and enter Play Mode first."); return; }
        if (Object.FindFirstObjectByType<MainAgentStressProbe>() != null)
        { Debug.LogWarning("A benchmark already exists. Restart Play Mode for a fresh measurement."); return; }
        new GameObject("TEMP - Agent performance probe").AddComponent<MainAgentStressProbe>();
    }
}
