using System;
using BlackoutProtocol.Body;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.AI.Hunter.Editor
{
    /// <summary>Applies only Hunter-owned runtime presentation components to Main.</summary>
    public static class HunterMainIntegrationSetup
    {
        public const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        [MenuItem("Tools/Hunter/Configure Main Hunter Debug + Locomotion")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before configuring Main.");
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            HunterBrain brain = Object.FindFirstObjectByType<HunterBrain>();
            if (brain == null) throw new InvalidOperationException("Main does not contain a HunterBrain.");
            Animator animator = brain.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
                throw new InvalidOperationException("Main Hunter has no configured Titan Animator.");

            AgentAnimatorDriver driver = brain.GetComponent<AgentAnimatorDriver>();
            if (driver == null) driver = Undo.AddComponent<AgentAnimatorDriver>(brain.gameObject);
            var driverSettings = new SerializedObject(driver);
            driverSettings.FindProperty("animator").objectReferenceValue = animator;
            driverSettings.FindProperty("referenceSpeed").floatValue = 4f;
            driverSettings.FindProperty("idleSpeedThreshold").floatValue = 0.02f;
            driverSettings.FindProperty("speedDampTime").floatValue = 0.1f;
            driverSettings.ApplyModifiedPropertiesWithoutUndo();

            HunterDebugPanel panel = brain.GetComponent<HunterDebugPanel>();
            if (panel == null) panel = Undo.AddComponent<HunterDebugPanel>(brain.gameObject);
            var panelSettings = new SerializedObject(panel);
            panelSettings.FindProperty("brain").objectReferenceValue = brain;
            panelSettings.FindProperty("pathfinder").objectReferenceValue = brain.GetComponent<HunterNavMeshPathfinder>();
            panelSettings.FindProperty("overlayVisible").boolValue = false;
            panelSettings.FindProperty("compareOnRouteRequests").boolValue = false;
            panelSettings.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(brain.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Main Hunter integration.");
            AssetDatabase.SaveAssets();
            Debug.Log("Configured Main Hunter locomotion driver and F1 debug panel without changing its brain, visual, or level geometry.");
        }
    }
}
