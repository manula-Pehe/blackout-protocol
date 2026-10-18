using BlackoutProtocol.AI.Sentinel;
using BlackoutProtocol.AI.Sentinel.Testing;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace BlackoutProtocol.Editor
{
    /// <summary>Rebuilds the dedicated Sentinel HSM test scene and its baked ground NavMesh.</summary>
    public static class SentinelTestSceneSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/Ashen/Sentinel_TestingGround.unity";
        private const string DronePrefabPath = "Assets/_Project/Prefabs/AI/Sentinel/SentinelDrone.prefab";
        private const string NavMeshAssetPath = "Assets/_Project/Scenes/Sandbox/Ashen/Sentinel_TestingGround_NavMesh.asset";

        [MenuItem("Tools/Sentinel/Rebuild HSM Testing Ground")]
        public static void Rebuild()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AssetDatabase.DeleteAsset(NavMeshAssetPath);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground — NavMesh Floor";
            floor.layer = 10;
            floor.transform.localScale = new Vector3(2f, 1f, 2f);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Occlusion Wall";
            wall.layer = 10;
            wall.transform.position = new Vector3(2f, 2.2f, 0f);
            wall.transform.localScale = new Vector3(0.45f, 4.4f, 8f);

            GameObject surfaceObject = new GameObject("Navigation Surface");
            surfaceObject.layer = 10;
            NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0f, 1.5f, 0f);
            surface.size = new Vector3(22f, 3.2f, 22f);
            surface.layerMask = 1 << 10;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                throw new System.InvalidOperationException("NavMesh bake produced no data.");
            }
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            EditorUtility.SetDirty(surface);

            GameObject dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrefabPath);
            if (dronePrefab == null) throw new System.InvalidOperationException("Could not load Sentinel drone prefab.");
            GameObject drone = (GameObject)PrefabUtility.InstantiatePrefab(dronePrefab, scene);
            drone.name = "Sentinel Drone — HSM Test";
            drone.layer = 7;
            SetLayerRecursively(drone.transform, 7);
            drone.transform.SetPositionAndRotation(new Vector3(-7f, 2.2f, 0f), Quaternion.LookRotation(Vector3.right));

            GameObject routeRoot = new GameObject("Patrol Route — HSM Test");
            Vector3[] routePoints =
            {
                new Vector3(-7f, 0f, -3f),
                new Vector3(-4f, 0f, -3f),
                new Vector3(-4f, 0f, 3f),
                new Vector3(-7f, 0f, 3f)
            };
            for (int i = 0; i < routePoints.Length; i++)
            {
                GameObject point = new GameObject($"Waypoint {i + 1:00}");
                point.transform.position = routePoints[i];
                point.transform.SetParent(routeRoot.transform, true);
            }

            SentinelDroneBrain brain = drone.GetComponent<SentinelDroneBrain>();
            SerializedObject serializedBrain = new SerializedObject(brain);
            serializedBrain.FindProperty("patrolRouteRoot").objectReferenceValue = routeRoot.transform;
            serializedBrain.ApplyModifiedPropertiesWithoutUndo();
            brain.enabled = false;
            brain.enabled = true;

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Test Player — Sentinel Scenario";
            player.layer = 6;
            player.transform.position = new Vector3(14f, 1f, 8f);
            Object.DestroyImmediate(player.GetComponent<Renderer>());
            player.AddComponent<SentinelTestPlayerState>();

            GameObject cameraObject = new GameObject("Scenario Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            cameraObject.transform.position = new Vector3(0f, 12f, -20f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.45f, 1f));

            GameObject lightObject = new GameObject("Scenario Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject runnerObject = new GameObject("Sentinel HSM Scenario Runner");
            SentinelHsmScenarioRunner runner = runnerObject.AddComponent<SentinelHsmScenarioRunner>();
            SerializedObject serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty("sentinel").objectReferenceValue = brain;
            serializedRunner.FindProperty("body").objectReferenceValue = drone.GetComponent<SentinelDroneBody>();
            serializedRunner.FindProperty("player").objectReferenceValue = player.GetComponent<SentinelTestPlayerState>();
            serializedRunner.FindProperty("occlusionWall").objectReferenceValue = wall.transform;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Sentinel HSM test scene rebuilt with a four-point patrol route and baked NavMesh.");
        }

        private static void SetLayerRecursively(Transform current, int layer)
        {
            current.gameObject.layer = layer;
            for (int i = 0; i < current.childCount; i++) SetLayerRecursively(current.GetChild(i), layer);
        }
    }
}
