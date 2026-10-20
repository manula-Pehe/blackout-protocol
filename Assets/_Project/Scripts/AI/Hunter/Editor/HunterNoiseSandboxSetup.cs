using System;
using System.IO;
using BlackoutProtocol.AI.Hunter.Testing;
using BlackoutProtocol.Body;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.AI.Hunter.Editor
{
    /// <summary>Separate Stage B scene, reusing Stage A geometry and its persisted NavMesh asset.</summary>
    public static class HunterNoiseSandboxSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity";

        [MenuItem("Tools/Hunter/Create Noise Response Sandbox")]
        public static void CreateSandbox()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("HunterNoiseSandbox already exists; open that scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(HunterSandboxSetup.ScenePath);
            // Save As before configuring Stage B. The original Stage A scene is left intact.
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not create HunterNoiseSandbox.");
            var driver = Object.FindFirstObjectByType<HunterSandboxRouteDriver>();
            driver.enabled = false;
            var brain = driver.gameObject.AddComponent<HunterBrain>();
            AddDebugPanel(brain);
            var pointsRoot = new GameObject("Hunter Wander Route");
            Vector3[] points = { new Vector3(-5, 0, 1), new Vector3(-7, 0, 4), new Vector3(-4, 0, -4) };
            var settings = new SerializedObject(brain);
            settings.FindProperty("body").objectReferenceValue = brain.GetComponent<AgentBody>();
            settings.FindProperty("pathfinder").objectReferenceValue = brain.GetComponent<HunterNavMeshPathfinder>();
            settings.FindProperty("pathDisplay").objectReferenceValue = Object.FindFirstObjectByType<LineRenderer>();
            var wander = settings.FindProperty("wanderPoints");
            wander.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject($"Wander Point {i + 1}");
                point.transform.SetParent(pointsRoot.transform);
                point.transform.position = points[i];
                wander.GetArrayElementAtIndex(i).objectReferenceValue = point.transform;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();

            var targetRoot = new GameObject("Hunter Sandbox Player Stand-in");
            targetRoot.transform.position = new Vector3(6, 0, -3);
            var target = targetRoot.AddComponent<HunterSandboxTarget>();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visible Damageable Target";
            visual.transform.SetParent(targetRoot.transform, false);
            visual.transform.localPosition = Vector3.up * 0.9f;
            visual.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            visual.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Scenes/Sandbox/Manula/HunterSandboxAssets/Goal.mat");
            targetRoot.SetActive(false);

            var trigger = new GameObject("Hunter Sandbox Noise Controls").AddComponent<HunterSandboxNoiseTrigger>();
            var triggerSettings = new SerializedObject(trigger);
            triggerSettings.FindProperty("hunter").objectReferenceValue = brain;
            triggerSettings.FindProperty("acrossWall").objectReferenceValue = driver.Destination;
            triggerSettings.FindProperty("testTarget").objectReferenceValue = target;
            triggerSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Hunter Stage B sandbox saved at {ScenePath}, reusing Stage A's baked NavMesh.");
        }

        [MenuItem("Tools/Hunter/Enable Debug Panel in Noise Sandbox")]
        public static void EnableDebugPanel()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before saving sandbox debug configuration.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            AddDebugPanel(Object.FindFirstObjectByType<HunterBrain>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AddDebugPanel(HunterBrain brain)
        {
            if (brain == null) throw new InvalidOperationException("HunterNoiseSandbox has no HunterBrain.");
            var panel = brain.GetComponent<HunterDebugPanel>();
            if (panel == null) panel = brain.gameObject.AddComponent<HunterDebugPanel>();
            var settings = new SerializedObject(panel);
            settings.FindProperty("brain").objectReferenceValue = brain;
            settings.FindProperty("pathfinder").objectReferenceValue = brain.GetComponent<HunterNavMeshPathfinder>();
            settings.FindProperty("overlayVisible").boolValue = true;
            settings.FindProperty("compareOnRouteRequests").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
