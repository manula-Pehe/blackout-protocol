using System;
using System.IO;
using System.Security.Cryptography;
using BlackoutProtocol.Core;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BlackoutProtocol.Editor
{
    /// <summary>Creates the World Builder's four-zone level without overwriting authored scenes.</summary>
    public static class WorldGreyboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string OutputFolder = "Assets/_Project/Materials/WorldGreybox";
        private const string ReceiptPath = OutputFolder + "/generation.json";
        private const string StartingSceneHash = "2afec877903f01bffe0bf436446e2d24be5b90ce9b21209a86724c02f9225dd4";
        private const string RootName = "Blackout Protocol - Generated Greybox v1";
        private const float WallHeight = 4f;
        private const float WallThickness = 0.4f;
        private const float PassageWidth = 4f;

        [Serializable]
        private sealed class Receipt
        {
            public string sceneHash;
            public string builderVersion;
        }

        /// <summary>Builds and saves Main once; subsequent unchanged builds do nothing.</summary>
        [MenuItem("Tools/Blackout Protocol/Build Greybox")]
        public static void Build()
        {
            try
            {
                BuildChecked();
            }
            catch (Exception exception)
            {
                Debug.LogError("Greybox generation stopped: " + exception.Message);
            }
        }

        private static void BuildChecked()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building.");

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save or discard unsaved scene changes first. No scenes were changed.");
            }

            if (!File.Exists(ScenePath))
                throw new InvalidOperationException("Main.unity is missing. No scene will be created or replaced.");

            string currentHash = HashScene();
            if (File.Exists(ReceiptPath))
            {
                Receipt receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(ReceiptPath));
                if (receipt == null || receipt.builderVersion != RootName || receipt.sceneHash != currentHash)
                    throw new InvalidOperationException("Main changed since generation. Preserve your work; the builder will not overwrite it.");

                Debug.Log("Greybox already generated and unchanged. Nothing was modified. Open " + ScenePath);
                return;
            }

            if (currentHash != StartingSceneHash)
                throw new InvalidOperationException("Main differs from the approved Camera/Light/Environment/01_LoadingBay starting scene. Refusing to change unexpected authored content.");

            if (Directory.Exists(OutputFolder) || File.Exists(OutputFolder))
                throw new InvalidOperationException("The reserved WorldGreybox asset folder already exists without a receipt. Inspect it before proceeding.");

            CheckLayer(GameLayers.Ground, "Ground");
            CheckLayer(GameLayers.Obstacle, "Obstacle");
            CheckLayer(GameLayers.Enemy, "Enemy");
            CheckLayer(GameLayers.Interactable, "Interactable");
            CheckLayer(GameLayers.Player, "Player");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (shader == null || font == null || playerPrefab == null)
                throw new InvalidOperationException("URP Lit shader, built-in font or existing Player prefab is unavailable.");
            foreach (Transform child in playerPrefab.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    throw new InvalidOperationException("Player prefab contains a missing script; ask its owner to repair it first.");
            }
            if (playerPrefab.GetComponent<CharacterController>() == null || playerPrefab.GetComponentInChildren<Camera>() == null)
                throw new InvalidOperationException("Player prefab needs its CharacterController and camera.");

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            ValidateStartingHierarchy(scene, out Transform environment, out Transform loadingBay, out Camera originalCamera);
            if (HashScene() != currentHash || scene.isDirty)
                throw new InvalidOperationException("Main changed while opening. No generation was started.");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Blackout Protocol Greybox");
            bool saved = false;
            try
            {
                SceneManager.SetActiveScene(scene);
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "WorldGreybox");
                Material floor = MaterialAsset("Floor", new Color(0.22f, 0.25f, 0.28f), shader);
                Material wall = MaterialAsset("Wall", new Color(0.32f, 0.35f, 0.38f), shader);
                Material cover = MaterialAsset("Cover", new Color(0.43f, 0.32f, 0.18f), shader);
                Material red = MaterialAsset("LoadingRed", new Color(0.65f, 0.14f, 0.12f), shader);
                Material blue = MaterialAsset("SecurityBlue", new Color(0.12f, 0.38f, 0.7f), shader);
                Material amber = MaterialAsset("MaintenanceAmber", new Color(0.8f, 0.55f, 0.1f), shader);
                Material cyan = MaterialAsset("CoreCyan", new Color(0.08f, 0.7f, 0.7f), shader);

                Transform root = Group(RootName, environment);
                Transform geometry = Group("Navigation Geometry", root);
                Transform loading = Room("01 Loading Bay", new Vector2(-24f, 0f), new Vector2(20f, 18f), geometry, floor, loadingBay);
                Transform hub = Room("02 Security Hub", Vector2.zero, new Vector2(24f, 24f), geometry, floor);
                Transform maintenance = Room("03 Maintenance Storage", new Vector2(12f, 30f), new Vector2(32f, 16f), geometry, floor);
                Transform core = Room("04 Control Core", new Vector2(30f, 0f), new Vector2(20f, 24f), geometry, floor);

                Boundary(loading, true, -34f, -9f, 9f, wall);
                Boundary(loading, true, -14f, -9f, 9f, wall, 0f);
                Boundary(loading, false, -9f, -34f, -14f, wall);
                Boundary(loading, false, 9f, -34f, -14f, wall);
                Boundary(hub, true, -12f, -12f, 12f, wall, 0f);
                Boundary(hub, true, 12f, -12f, 12f, wall, 0f);
                Boundary(hub, false, -12f, -12f, 12f, wall);
                Boundary(hub, false, 12f, -12f, 12f, wall, 0f);
                Boundary(maintenance, true, -4f, 22f, 38f, wall);
                Boundary(maintenance, true, 28f, 22f, 38f, wall);
                Boundary(maintenance, false, 22f, -4f, 28f, wall, 0f, 26f);
                Boundary(maintenance, false, 38f, -4f, 28f, wall);
                Boundary(core, true, 20f, -12f, 12f, wall, 0f);
                Boundary(core, true, 40f, -12f, 12f, wall);
                Boundary(core, false, -12f, 20f, 40f, wall);
                Boundary(core, false, 12f, 20f, 40f, wall, 26f);

                Corridor("Loading to Hub", new Vector2(-13f, 0f), new Vector2(2f, PassageWidth), true, geometry, floor, wall);
                Corridor("Hub to Core - Direct", new Vector2(16f, 0f), new Vector2(8f, PassageWidth), true, geometry, floor, wall);
                Corridor("Hub to Maintenance", new Vector2(0f, 17f), new Vector2(PassageWidth, 10f), false, geometry, floor, wall);
                Corridor("Maintenance to Core", new Vector2(26f, 17f), new Vector2(PassageWidth, 10f), false, geometry, floor, wall);

                ZoneDetails(loading, new Vector2(-24f, 0f), "01  LOADING BAY", red, font);
                ZoneDetails(hub, Vector2.zero, "02  SECURITY HUB", blue, font);
                ZoneDetails(maintenance, new Vector2(12f, 30f), "03  MAINTENANCE / STORAGE", amber, font);
                ZoneDetails(core, new Vector2(30f, 0f), "04  CONTROL CORE", cyan, font);
                CoverSet(loading, cover, new Vector2(-28f, -5f), new Vector2(-22f, 4f), new Vector2(-18f, -5f));
                CoverSet(hub, cover, new Vector2(-7f, -6f), new Vector2(0f, -6f), new Vector2(7f, -6f), new Vector2(-7f, 6f), new Vector2(0f, 6f), new Vector2(7f, 6f));
                CoverSet(maintenance, cover, new Vector2(3f, 28f), new Vector2(12f, 33f), new Vector2(21f, 28f));
                CoverSet(core, cover, new Vector2(24f, -6f), new Vector2(31f, -6f), new Vector2(35f, 6f));
                Box("Power Core Landmark", core, new Vector3(32f, 2f, 1f), new Vector3(2f, 4f, 2f), cyan, GameLayers.Obstacle);

                Transform markers = Group("Spawn Markers - no AI", root);
                Transform spawn = Marker("Player Spawn", markers, new Vector3(-30f, 1.1f, 0f), GameLayers.Player);
                spawn.rotation = Quaternion.Euler(0f, 90f, 0f);
                Marker("Enemy Spawn - Loading Bay", markers, new Vector3(-18f, 0f, 5f), GameLayers.Enemy);
                Marker("Enemy Spawn - Security Hub", markers, new Vector3(8f, 0f, 8f), GameLayers.Enemy);
                Marker("Enemy Spawn - Maintenance", markers, new Vector3(20f, 0f, 35f), GameLayers.Enemy);
                Marker("Enemy Spawn - Control Core", markers, new Vector3(36f, 0f, 8f), GameLayers.Enemy);
                Marker("Shutdown Console Placement", markers, new Vector3(37f, 0f, -8f), GameLayers.Interactable);
                Marker("Elevator Exit Placement", markers, new Vector3(37f, 0f, 0f), GameLayers.Interactable);

                GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
                Undo.RegisterCreatedObjectUndo(player, "Create greybox player");
                player.transform.SetParent(root, true);
                player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = GameLayers.Player;

                NavMeshSurface surface = Undo.AddComponent<NavMeshSurface>(environment.gameObject);
                surface.agentTypeID = 0;
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = (1 << GameLayers.Ground) | (1 << GameLayers.Obstacle);
                AudioListener originalListener = originalCamera.GetComponent<AudioListener>();
                Undo.RecordObjects(new UnityEngine.Object[] { originalCamera, originalListener }, "Use generated player's camera and listener");
                originalCamera.enabled = false;
                originalListener.enabled = false;

                if (HashScene() != currentHash)
                    throw new InvalidOperationException("Main changed on disk during generation. It was not saved.");
                Undo.FlushUndoRecordObjects();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Unity could not save Main. Generated assets were retained for inspection.");
                saved = true;
                Undo.CollapseUndoOperations(undoGroup);

                File.WriteAllText(ReceiptPath, JsonUtility.ToJson(new Receipt { sceneHash = HashScene(), builderVersion = RootName }, true));
                AssetDatabase.ImportAsset(ReceiptPath);
                Selection.activeGameObject = root.gameObject;
                Debug.Log("Saved four-zone greybox to Main: 15 CoverPoints, four enemy markers, two Hub/Core routes. Bake Navigation Geometry's NavMeshSurface before testing AI paths.");
            }
            catch
            {
                if (!saved)
                {
                    Undo.FlushUndoRecordObjects();
                    Undo.RevertAllDownToGroup(undoGroup);
                }
                throw;
            }
        }

        private static void ValidateStartingHierarchy(Scene scene, out Transform environment, out Transform loadingBay, out Camera originalCamera)
        {
            environment = null;
            loadingBay = null;
            originalCamera = null;
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Length != 3)
                throw new InvalidOperationException("Expected exactly Main Camera, Directional Light and Environment roots.");

            foreach (GameObject root in roots)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root) || !root.activeSelf)
                    throw new InvalidOperationException("Starting roots must be active scene objects, not prefab instances.");
                if (root.name == "Environment" && environment == null)
                {
                    environment = root.transform;
                    if (root.GetComponents<Component>().Length != 1 || environment.childCount != 1 ||
                        environment.localPosition != new Vector3(0f, 3f, 0f) ||
                        environment.localRotation != Quaternion.identity || environment.localScale != Vector3.one)
                        throw new InvalidOperationException("Environment must have only Transform at (0,3,0), identity rotation/scale and the approved single child.");
                    loadingBay = environment.GetChild(0);
                    if (loadingBay.name != "01_LoadingBay" || !loadingBay.gameObject.activeSelf ||
                        PrefabUtility.IsPartOfPrefabInstance(loadingBay.gameObject) ||
                        loadingBay.GetComponents<Component>().Length != 1 || loadingBay.childCount != 0 ||
                        loadingBay.localPosition != Vector3.zero || loadingBay.localRotation != Quaternion.identity ||
                        loadingBay.localScale != Vector3.one)
                        throw new InvalidOperationException("01_LoadingBay must be the empty Transform-only child with identity local transform.");
                }
                else if (root.name == "Main Camera" && originalCamera == null)
                {
                    originalCamera = root.GetComponent<Camera>();
                    if (originalCamera == null || root.GetComponent<AudioListener>() == null || root.transform.childCount != 0 ||
                        root.GetComponents<Component>().Length != 4 ||
                        root.GetComponent<UniversalAdditionalCameraData>() == null)
                        throw new InvalidOperationException("Main Camera contains unexpected or missing components.");
                }
                else if (root.name != "Directional Light" || root.GetComponent<Light>() == null ||
                    root.GetComponents<Component>().Length != 3 || root.transform.childCount != 0 ||
                    root.GetComponent<UniversalAdditionalLightData>() == null)
                    throw new InvalidOperationException("Unexpected starting scene object or component: " + root.name);
            }

            if (environment == null || loadingBay == null || originalCamera == null)
                throw new InvalidOperationException("The approved starting hierarchy is incomplete.");
        }

        private static void CheckLayer(int layer, string expected)
        {
            if (LayerMask.LayerToName(layer) != expected)
                throw new InvalidOperationException($"Expected layer {layer} to be {expected}. Project settings were not changed.");
        }

        private static string HashScene()
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(ScenePath))).Replace("-", "").ToLowerInvariant();
        }

        private static Material MaterialAsset(string name, Color color, Shader shader)
        {
            Material material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, OutputFolder + "/" + name + ".mat");
            return material;
        }

        private static Transform Group(string name, Transform parent)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, true);
            Undo.RegisterCreatedObjectUndo(group.gameObject, "Create greybox group");
            return group;
        }

        private static Transform Marker(string name, Transform parent, Vector3 position, int layer)
        {
            Transform marker = Group(name, parent);
            marker.position = position;
            marker.gameObject.layer = layer;
            return marker;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, int layer)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.layer = layer;
            box.GetComponent<Renderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            Undo.RegisterCreatedObjectUndo(box, "Create greybox geometry");
            return box;
        }

        private static Transform Room(string name, Vector2 center, Vector2 size, Transform parent, Material floor, Transform existing = null)
        {
            Transform room = existing != null ? existing : Group(name, parent);
            Box("Floor", room, new Vector3(center.x, -0.25f, center.y), new Vector3(size.x, 0.5f, size.y), floor, GameLayers.Ground);
            return room;
        }

        private static void Boundary(Transform parent, bool vertical, float fixedCoordinate, float start, float end, Material wall, params float[] openings)
        {
            float cursor = start;
            foreach (float opening in openings)
            {
                WallSegment(parent, vertical, fixedCoordinate, cursor, opening - PassageWidth * 0.5f, wall);
                cursor = opening + PassageWidth * 0.5f;
            }
            WallSegment(parent, vertical, fixedCoordinate, cursor, end, wall);
        }

        private static void WallSegment(Transform parent, bool vertical, float fixedCoordinate, float start, float end, Material wall)
        {
            if (end - start < 0.01f) return;
            float midpoint = (start + end) * 0.5f;
            Vector3 position = vertical ? new Vector3(fixedCoordinate, WallHeight * 0.5f, midpoint) : new Vector3(midpoint, WallHeight * 0.5f, fixedCoordinate);
            Vector3 size = vertical ? new Vector3(WallThickness, WallHeight, end - start) : new Vector3(end - start, WallHeight, WallThickness);
            Box("Wall", parent, position, size, wall, GameLayers.Obstacle);
        }

        private static void Corridor(string name, Vector2 center, Vector2 size, bool horizontal, Transform parent, Material floor, Material wall)
        {
            Transform corridor = Room(name, center, size, parent, floor);
            if (horizontal)
            {
                Boundary(corridor, false, center.y - size.y * 0.5f, center.x - size.x * 0.5f, center.x + size.x * 0.5f, wall);
                Boundary(corridor, false, center.y + size.y * 0.5f, center.x - size.x * 0.5f, center.x + size.x * 0.5f, wall);
            }
            else
            {
                Boundary(corridor, true, center.x - size.x * 0.5f, center.y - size.y * 0.5f, center.y + size.y * 0.5f, wall);
                Boundary(corridor, true, center.x + size.x * 0.5f, center.y - size.y * 0.5f, center.y + size.y * 0.5f, wall);
            }
        }

        private static void CoverSet(Transform zone, Material material, params Vector2[] positions)
        {
            for (int index = 0; index < positions.Length; index++)
            {
                Vector2 position = positions[index];
                Box($"Cover Block {index + 1:00}", zone, new Vector3(position.x, 0.75f, position.y), new Vector3(2.4f, 1.5f, 1.2f), material, GameLayers.Obstacle);
                Transform point = Marker($"Cover Point {index + 1:00}", zone, new Vector3(position.x, 0f, position.y - 1.3f), 0);
                point.gameObject.AddComponent<CoverPoint>();
            }
        }

        private static void ZoneDetails(Transform parent, Vector2 center, string title, Material accent, Font font)
        {
            Box("Zone Identification Stripe", parent, new Vector3(center.x, 0.015f, center.y - 3f), new Vector3(5f, 0.03f, 0.3f), accent, GameLayers.Ground);
            Transform label = Marker(title, parent, new Vector3(center.x, 0.04f, center.y), 0);
            label.rotation = Quaternion.Euler(90f, 0f, 0f);
            TextMesh text = label.gameObject.AddComponent<TextMesh>();
            text.font = font;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            text.text = title;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.18f;
            text.fontSize = 48;
            text.color = Color.white;

            Transform lightTransform = Marker("Zone Light", parent, new Vector3(center.x, 3.5f, center.y), 0);
            Light light = lightTransform.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 24f;
            light.intensity = 3f;
            light.color = Color.Lerp(Color.white, accent.color, 0.25f);
        }
    }
}
