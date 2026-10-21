using System;
using System.IO;
using BlackoutProtocol.AI.Hunter.Testing;
using BlackoutProtocol.Body;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.AI.Hunter.Editor
{
    /// <summary>Save-As composition only. Never rebakes or overwrites the original sandboxes or visual assets.</summary>
    public static class ManulaTechnicalDemoSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/Sandbox/Manula/ManulaTechnicalDemo.unity";
        private const string VisualPath = "Assets/_Project/Animations/AgentBody/Titan/TitanVisual.prefab";

        [MenuItem("Tools/Hunter/Create Manula Technical Demo")]
        public static void CreateDemo()
        {
            if (Application.isPlaying || File.Exists(ScenePath))
                throw new InvalidOperationException("Exit Play Mode; the technical demo must not already exist. Open the saved scene instead.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RequireScriptBinding<AgentBody>("Assets/_Project/Scripts/AgentBody/AgentBody.cs");
            RequireScriptBinding<AgentAnimatorDriver>("Assets/_Project/Scripts/AgentBody/AgentAnimatorDriver.cs");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            if (prefab == null) throw new InvalidOperationException("Missing Manula-owned TitanVisual prefab.");
            var scene = EditorSceneManager.OpenScene(HunterNoiseSandboxSetup.ScenePath);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not Save As the technical demo.");
            HunterBrain hunter = Object.FindFirstObjectByType<HunterBrain>();
            HunterSandboxNoiseTrigger signals = Object.FindFirstObjectByType<HunterSandboxNoiseTrigger>();
            Camera camera = Object.FindFirstObjectByType<Camera>();
            NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (hunter == null || signals == null || signals.TestTarget == null || camera == null || surface == null || surface.navMeshData == null)
                throw new InvalidOperationException("HunterNoiseSandbox is missing its proven runtime, target, camera or saved navigation data.");

            // Only the primitive visual children in the newly copied scene are replaced.
            for (int i = hunter.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(hunter.transform.GetChild(i).gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.transform.SetParent(hunter.transform, false);
            hunter.gameObject.name = "Hunter / Titan / Utility AI + AgentBody";
            var animation = hunter.gameObject.AddComponent<AgentAnimatorDriver>();
            var animationSettings = new SerializedObject(animation);
            animationSettings.FindProperty("m_Script").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Scripts/AgentBody/AgentAnimatorDriver.cs");
            animationSettings.FindProperty("animator").objectReferenceValue = visual.GetComponentInChildren<Animator>();
            animationSettings.FindProperty("referenceSpeed").floatValue = 4;
            animationSettings.FindProperty("speedDampTime").floatValue = 0.1f;
            animationSettings.ApplyModifiedPropertiesWithoutUndo();
            if (hunter.TryGetComponent(out HunterSandboxRouteDriver driver)) Object.DestroyImmediate(driver);

            var signalSettings = new SerializedObject(signals);
            signalSettings.FindProperty("showControls").boolValue = false; // Same methods, new visible presentation.
            signalSettings.ApplyModifiedPropertiesWithoutUndo();
            HunterDebugPanel panel = hunter.GetComponent<HunterDebugPanel>();
            var panelSettings = new SerializedObject(panel);
            panelSettings.FindProperty("compactDemoLayout").boolValue = true;
            panelSettings.FindProperty("overlayVisible").boolValue = true;
            panelSettings.FindProperty("compareOnRouteRequests").boolValue = true;
            panelSettings.ApplyModifiedPropertiesWithoutUndo();

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.49f, 0.56f);
            var fill = new GameObject("Titan fill light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.65f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(35, 145, 0);
            var demo = new GameObject("Manula / GV + IS / Technical demonstration").AddComponent<ManulaTechnicalDemo>();
            var demoSettings = new SerializedObject(demo);
            demoSettings.FindProperty("hunter").objectReferenceValue = hunter;
            demoSettings.FindProperty("signals").objectReferenceValue = signals;
            demoSettings.FindProperty("animationDriver").objectReferenceValue = animation;
            demoSettings.FindProperty("viewCamera").objectReferenceValue = camera;
            demoSettings.ApplyModifiedPropertiesWithoutUndo();

            // Geometry, wander points, simulated target, brain parameters and baked
            // NavMesh reference remain exactly those of the source sandbox.
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the technical demo.");
            AssetDatabase.SaveAssets();
            Debug.Log("Manula technical demo saved: " + ScenePath + ". Existing geometry/NavMesh and original scenes were not modified.");
        }

        private static void RequireScriptBinding<T>(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass() != typeof(T))
                throw new InvalidOperationException("Unity has not finished importing " + path + ". Reimport it and allow script reload before creating the demo.");
        }
    }
}
