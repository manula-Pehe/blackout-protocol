using System;
using System.IO;
using BlackoutProtocol.AI.Flanker;
using BlackoutProtocol.Body;
using BlackoutProtocol.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackoutProtocol.Editor
{
    /// <summary>Creates an isolated demonstration prefab, refusing to overwrite any existing output.</summary>
    public static class FlankerPrefabBuilder
    {
        private const string Folder = "Assets/_Project/Prefabs/AI/Flanker";
        private const string PrefabPath = Folder + "/Flanker.prefab";

        [MenuItem("Tools/Blackout Protocol/Create Flanker Prefab")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("Exit Play Mode and Prefab Mode before creating the Flanker prefab.");
                return;
            }
            if (Directory.Exists(Folder) || File.Exists(Folder))
            {
                Debug.LogWarning("Flanker output already exists. Nothing was overwritten. Inspect " + PrefabPath);
                return;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || LayerMask.LayerToName(GameLayers.Enemy) != "Enemy")
            {
                Debug.LogError("Flanker requires the existing URP Lit shader and Enemy layer 7.");
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(temporary);
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs/AI", "Flanker");
                var material = new Material(shader) { name = "Flanker Blue", color = new Color(0.08f, 0.3f, 0.9f) };
                AssetDatabase.CreateAsset(material, Folder + "/FlankerBlue.mat");
                var root = new GameObject("Flanker Android");
                root.SetActive(false);
                root.layer = GameLayers.Enemy;
                root.tag = GameTags.Enemy;
                CharacterController controller = root.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = 0.35f;
                controller.center = Vector3.up * 0.9f;
                controller.stepOffset = 0.3f;
                root.AddComponent<FlankerHealth>();
                root.AddComponent<FlankerNavMeshPathfinder>();
                AgentBody body = root.AddComponent<AgentBody>();
                var serializedBody = new SerializedObject(body);
                serializedBody.FindProperty("waypointAcceptanceRadius").floatValue = 0.1f;
                serializedBody.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<FlankerBrain>();
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Blue Android Placeholder";
                visual.layer = GameLayers.Enemy;
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.up * 0.9f;
                visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                visual.GetComponent<Collider>().enabled = false;
                visual.GetComponent<Renderer>().sharedMaterial = material;
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success) throw new IOException("Unity did not save the Flanker prefab. Partial output was retained for inspection.");
                Debug.Log("Created " + PrefabPath + ". Drag it into a demo scene at a ground-level spawn; no existing scene was saved or modified.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Flanker prefab generation stopped: " + exception.Message);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(temporary, true);
            }
        }
    }
}
