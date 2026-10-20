using System;
using System.IO;
using BlackoutProtocol.AI.Hunter.Testing;
using BlackoutProtocol.Body;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.AI.Hunter.Editor
{
    /// <summary>Creates only the Hunter sandbox assets; existing scene assets are never replaced.</summary>
    public static class HunterSandboxSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/Sandbox/Manula/HunterSandbox.unity";
        private const string AssetFolder = "Assets/_Project/Scenes/Sandbox/Manula/HunterSandboxAssets";
        private const string NavMeshPath = AssetFolder + "/HunterSandboxNavMesh.asset";

        [MenuItem("Tools/Hunter/Create Navigation Sandbox")]
        public static void CreateSandbox()
        {
            if (File.Exists(ScenePath) || AssetDatabase.IsValidFolder(AssetFolder))
                throw new InvalidOperationException("Hunter sandbox already exists. Open it instead; use Bake Open Hunter Sandbox after changing geometry.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.CreateFolder("Assets/_Project/Scenes/Sandbox/Manula", "HunterSandboxAssets");
            Material floorMaterial = Material("Floor", new Color(0.16f, 0.22f, 0.29f));
            Material wallMaterial = Material("Obstacle", new Color(0.95f, 0.43f, 0.14f));
            Material robotMaterial = Material("Hunter", new Color(0.12f, 0.72f, 0.8f));
            Material darkMaterial = Material("RobotTrim", new Color(0.045f, 0.065f, 0.09f));
            Material goalMaterial = Material("Goal", new Color(0.22f, 0.95f, 0.42f));
            Material routeMaterial = Material("Route", Color.cyan, true);

            var geometry = new GameObject("Baked Navigation Geometry");
            Cube("Floor", geometry.transform, new Vector3(0, -0.25f, 0), new Vector3(20, 0.5f, 16), floorMaterial);
            Cube("Corner Obstacle", geometry.transform, new Vector3(0, 1.4f, -3), new Vector3(1.2f, 2.8f, 10), wallMaterial);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.agentTypeID = 0;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            surface.minRegionArea = 0.5f;

            var goal = new GameObject("Goal — Green Landing Pad");
            goal.transform.position = new Vector3(6, 0, -3);
            Visual(PrimitiveType.Cylinder, "Goal Marker", goal.transform, new Vector3(0, 0.025f, 0), new Vector3(1.6f, 0.025f, 1.6f), goalMaterial);
            var start = new GameObject("Start — Cyan Landing Pad");
            start.transform.position = new Vector3(-6, 0, -3);
            Visual(PrimitiveType.Cylinder, "Start Marker", start.transform, new Vector3(0, 0.025f, 0), new Vector3(1.6f, 0.025f, 1.6f), robotMaterial);

            var hunter = new GameObject("Hunter — A Star Navigation Test Robot");
            hunter.transform.position = start.transform.position + Vector3.up * 0.05f;
            hunter.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var controller = hunter.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = Vector3.up * 0.9f;
            controller.skinWidth = 0.03f;
            controller.stepOffset = 0.25f;
            controller.minMoveDistance = 0;
            hunter.AddComponent<AgentMotor>();
            hunter.AddComponent<AgentRotation>();
            var body = hunter.AddComponent<AgentBody>();
            var bodySettings = new SerializedObject(body);
            bodySettings.FindProperty("waypointAcceptanceRadius").floatValue = 0.12f;
            bodySettings.FindProperty("arrivalRadius").floatValue = 0.12f;
            bodySettings.ApplyModifiedPropertiesWithoutUndo();
            var pathfinder = hunter.AddComponent<HunterNavMeshPathfinder>();
            Visual(PrimitiveType.Cube, "Torso", hunter.transform, new Vector3(0, 0.95f, 0), new Vector3(0.65f, 0.8f, 0.5f), robotMaterial);
            Visual(PrimitiveType.Cube, "Head", hunter.transform, new Vector3(0, 1.55f, 0.05f), new Vector3(0.62f, 0.38f, 0.48f), darkMaterial);
            Visual(PrimitiveType.Cube, "Sensor Visor", hunter.transform, new Vector3(0, 1.57f, 0.3f), new Vector3(0.48f, 0.12f, 0.05f), goalMaterial);
            Visual(PrimitiveType.Cube, "Left Tread", hunter.transform, new Vector3(-0.28f, 0.28f, 0), new Vector3(0.2f, 0.45f, 0.65f), darkMaterial);
            Visual(PrimitiveType.Cube, "Right Tread", hunter.transform, new Vector3(0.28f, 0.28f, 0), new Vector3(0.2f, 0.45f, 0.65f), darkMaterial);

            var route = new GameObject("Hunter A Star Route — Cyan").AddComponent<LineRenderer>();
            route.sharedMaterial = routeMaterial;
            route.useWorldSpace = true;
            route.startWidth = route.endWidth = 0.075f;
            route.positionCount = 0;
            route.numCornerVertices = 4;
            var driver = hunter.AddComponent<HunterSandboxRouteDriver>();
            var settings = new SerializedObject(driver);
            settings.FindProperty("body").objectReferenceValue = body;
            settings.FindProperty("pathfinder").objectReferenceValue = pathfinder;
            settings.FindProperty("navigationSurface").objectReferenceValue = surface;
            settings.FindProperty("destination").objectReferenceValue = goal.transform;
            settings.FindProperty("pathDisplay").objectReferenceValue = route;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 22, -16);
            camera.transform.LookAt(new Vector3(0, 0, -0.5f));
            camera.orthographic = true;
            camera.orthographicSize = 10.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Sandbox Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.7f);

            Bake(surface);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save HunterSandbox.");
            AssetDatabase.SaveAssets();
            Debug.Log($"Hunter sandbox created: {ScenePath}; persisted NavMesh: {NavMeshPath}");
        }

        [MenuItem("Tools/Hunter/Bake Open Hunter Sandbox")]
        public static void BakeOpenSandbox()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || Application.isPlaying)
                throw new InvalidOperationException("Open HunterSandbox in Edit Mode before baking.");
            NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null) throw new InvalidOperationException("HunterSandbox is missing its Navigation Surface.");
            Bake(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void Bake(NavMeshSurface surface)
        {
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            NavMeshData built = surface.navMeshData;
            if (built == null || NavMesh.CalculateTriangulation().indices.Length == 0)
                throw new InvalidOperationException("Hunter NavMesh bake produced no walkable triangles.");
            NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            if (existing == null) AssetDatabase.CreateAsset(built, NavMeshPath);
            else
            {
                surface.RemoveData();
                EditorUtility.CopySerialized(built, existing);
                surface.navMeshData = existing;
                surface.AddData();
                Object.DestroyImmediate(built);
                EditorUtility.SetDirty(existing);
            }
            EditorUtility.SetDirty(surface);
        }

        private static Material Material(string name, Color color, bool unlit = false)
        {
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The project's URP shader is unavailable.");
            var material = new Material(shader) { name = "HunterSandbox_" + name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, AssetFolder + "/" + name + ".mat");
            return material;
        }

        private static void Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Visual(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
