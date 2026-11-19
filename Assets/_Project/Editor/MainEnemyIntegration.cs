using System;
using System.Linq;
using BlackoutProtocol.AI.Breacher;
using BlackoutProtocol.AI.Hunter;
using BlackoutProtocol.AI.Sentinel;
using BlackoutProtocol.Body;
using BlackoutProtocol.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace BlackoutProtocol.Editor
{
    /// <summary>Adds the three missing AI roles to the authored Main scene without rebuilding its world.</summary>
    public static class MainEnemyIntegration
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string RootName = "Integrated Enemies";

        [MenuItem("Tools/Blackout Protocol/Integrate Missing Enemies")]
        public static void Integrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1 ||
                SceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogError("Open Main.unity alone in Edit Mode before integrating enemies.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.isDirty)
            {
                Debug.LogError("Save Main's current changes before integrating enemies.");
                return;
            }

            PlayerState[] players = FindSceneComponents<PlayerState>(scene);
            if (players.Length != 1 || players[0].GetComponent<PlayerHealth>() == null)
            {
                Debug.LogError("Main needs exactly one PlayerState with PlayerHealth.");
                return;
            }

            Transform security = FindUniqueMarker(scene, "Enemy Spawn - Security Hub");
            Transform maintenance = FindUniqueMarker(scene, "Enemy Spawn - Maintenance");
            Transform core = FindUniqueMarker(scene, "Enemy Spawn - Control Core");
            Transform loading = FindUniqueMarker(scene, "Enemy Spawn - Loading Bay");
            if (security == null || maintenance == null || core == null || loading == null) return;

            GameObject hunterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Hunter.prefab");
            GameObject breacherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/AI/Breacher/Breacher.prefab");
            GameObject sentinelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/AI/Sentinel/SentinelDrone.prefab");
            if (hunterPrefab == null || breacherPrefab == null || sentinelPrefab == null ||
                sentinelPrefab.GetComponent<SentinelDroneBrain>() == null || sentinelPrefab.GetComponent<SentinelDroneBody>() == null)
            {
                Debug.LogError("One of the Hunter, Breacher or Sentinel prefabs is missing/incomplete.");
                return;
            }

            if (!TryGround(security.position, out Vector3 hunterPosition) ||
                !TryGround(maintenance.position, out Vector3 breacherPosition) ||
                !TryGround(core.position, out Vector3 sentinelPosition))
            {
                Debug.LogError("An enemy marker is off the baked NavMesh. Run Audit Navigation first.");
                return;
            }
            Transform cover = scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name.StartsWith("Cover Point "))
                .OrderBy(item => (item.position - breacherPosition).sqrMagnitude)
                .FirstOrDefault();
            if (cover == null)
            {
                Debug.LogError("Main needs at least one cover point for the Breacher.");
                return;
            }

            if (FindSceneComponents<HunterBrain>(scene).Length > 0 ||
                FindSceneComponents<BreacherAgent>(scene).Length > 0 ||
                FindSceneComponents<SentinelDroneBrain>(scene).Length > 0 ||
                scene.GetRootGameObjects().Any(root => root.name == RootName))
            {
                Debug.LogWarning("Enemy integration already exists or has been customized. No duplicate was added.");
                return;
            }

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Integrate Missing Enemies");
            try
            {
                var root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, RootName);
                SceneManager.MoveGameObjectToScene(root, scene);

                GameObject hunter = Instantiate(hunterPrefab, root.transform, "Hunter", hunterPosition + Vector3.up);
                AddGroundBody(hunter);
                Undo.AddComponent<HunterNavMeshPathfinder>(hunter);
                Undo.AddComponent<HunterHealth>(hunter);
                HunterBrain hunterBrain = Undo.AddComponent<HunterBrain>(hunter);
                var hunterData = new SerializedObject(hunterBrain);
                SerializedProperty wander = hunterData.FindProperty("wanderPoints");
                wander.arraySize = 3;
                wander.GetArrayElementAtIndex(0).objectReferenceValue = loading;
                wander.GetArrayElementAtIndex(1).objectReferenceValue = maintenance;
                wander.GetArrayElementAtIndex(2).objectReferenceValue = core;
                hunterData.ApplyModifiedPropertiesWithoutUndo();

                GameObject breacher = Instantiate(breacherPrefab, root.transform, "Breacher", breacherPosition + Vector3.up);
                AddGroundBody(breacher);
                if (breacher.GetComponent<BreacherHealth>() == null)
                    Undo.AddComponent<BreacherHealth>(breacher);
                BreacherAgent breacherAgent = breacher.GetComponent<BreacherAgent>();
                if (breacherAgent == null) breacherAgent = Undo.AddComponent<BreacherAgent>(breacher);
                var breacherData = new SerializedObject(breacherAgent);
                breacherData.FindProperty("player").objectReferenceValue = players[0].transform;
                breacherData.FindProperty("coverPoint").objectReferenceValue = cover;
                // Main has no door/barricade route yet. The Breacher's optional-obstacle
                // GOAP mode pursues the player directly; its sandbox retains both actions.
                breacherData.ApplyModifiedPropertiesWithoutUndo();

                GameObject sentinel = Instantiate(sentinelPrefab, root.transform, "Sentinel Drone", sentinelPosition + Vector3.up * 2.2f);
                SetLayerRecursive(sentinel, LayerMask.NameToLayer("Enemy"));

                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("Integrated Hunter, Breacher and Sentinel in Main. Save the scene, then Play and verify all four AI agents.");
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogError("Enemy integration rolled back: " + exception);
            }
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent, string name, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Create " + name);
            instance.name = name;
            instance.transform.position = position;
            SetLayerRecursive(instance, LayerMask.NameToLayer("Enemy"));
            return instance;
        }

        private static void AddGroundBody(GameObject agent)
        {
            foreach (Collider collider in agent.GetComponentsInChildren<Collider>(true))
                Undo.DestroyObjectImmediate(collider);
            CharacterController controller = Undo.AddComponent<CharacterController>(agent);
            controller.center = Vector3.zero;
            controller.height = 2f;
            controller.radius = 0.35f;
            if (agent.GetComponent<AgentMotor>() == null) Undo.AddComponent<AgentMotor>(agent);
            if (agent.GetComponent<AgentRotation>() == null) Undo.AddComponent<AgentRotation>(agent);
            if (agent.GetComponent<AgentBody>() == null) Undo.AddComponent<AgentBody>(agent);
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            if (layer < 0) return;
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                transform.gameObject.layer = layer;
        }

        private static bool TryGround(Vector3 position, out Vector3 ground)
        {
            bool found = NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas);
            ground = hit.position;
            return found;
        }

        private static Transform FindUniqueMarker(Scene scene, string name)
        {
            Transform[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == name).ToArray();
            if (matches.Length == 1) return matches[0];
            Debug.LogError("Expected exactly one scene marker named " + name + "; found " + matches.Length + ".");
            return null;
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
