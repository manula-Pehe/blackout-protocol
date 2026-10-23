using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using BlackoutProtocol.GameFlow;
using BlackoutProtocol.Interaction;
using BlackoutProtocol.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackoutProtocol.Editor
{
    /// <summary>Adds an isolated, non-blocking game-flow slice to Main without opening, saving or rebuilding it.</summary>
    public static class GameFlowSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string ParentPath = "Environment/Blackout Protocol - Generated Greybox v1/Spawn Markers - no AI";
        private const string RootName = "Blackout Protocol - Game Flow v1";
        private const string PrefabPath = "Assets/_Project/Prefabs/Interaction/ShutDownConsole.prefab";
        private static readonly Vector3 ConsoleScale = new Vector3(0.65f, 0.8f, 0.35f);
        private static readonly Vector3 LabelOffset = new Vector3(-0.1f, 2.2f, 0f);

        /// <summary>Checks scene/marker ownership and adds only new objects; a valid repeat run changes nothing.</summary>
        [MenuItem("Tools/Blackout Protocol/Setup Game Flow")]
        public static void Setup()
        {
            try { SetupChecked(); }
            catch (Exception exception) { Debug.LogError("Setup Game Flow stopped: " + exception.Message); }
        }

        private static void SetupChecked()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && PrefabStageUtility.GetCurrentPrefabStage() == null, "Exit Play/Prefab Mode first.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            Require(scene.IsValid() && scene.isLoaded && SceneManager.sceneCount == 1 && SceneManager.GetActiveScene() == scene,
                "Open Main.unity alone and make it active. No scene was switched.");
            Transform parent = FindPath(scene, ParentPath);
            Require(parent.GetComponents<Component>().Length == 1 && parent.gameObject.activeInHierarchy && !PrefabUtility.IsPartOfPrefabInstance(parent), "The marker container has unexpected authored components or state.");
            Transform consoleMarker = UniqueChild(parent, "Shutdown Console Placement", true);
            Transform exitMarker = UniqueChild(parent, "Elevator Exit Placement", true);
            CheckMarker(consoleMarker, new Vector3(37f, 0f, -8f));
            CheckMarker(exitMarker, new Vector3(37f, 0f, 0f));
            List<PlayerState> players = Components<PlayerState>(scene);
            Require(players.Count == 1, "Exactly one existing PlayerState is required; no player is created or replaced.");
            PlayerState player = players[0];
            Require(player.gameObject.activeInHierarchy && player.enabled && player.GetComponent<PlayerHealth>() != null &&
                player.GetComponent<PlayerInteractor>() != null && player.GetComponent<PlayerCombatHud>() != null, "The existing player health/interactor/combat HUD must be present.");
            var interactor = new SerializedObject(player.GetComponent<PlayerInteractor>());
            Require((interactor.FindProperty("interactionMask").intValue & (1 << 8)) != 0, "The player's existing interaction mask must include layer 8.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab != null && prefab.GetComponent<ShutdownConsole>() != null && prefab.GetComponent<BoxCollider>() != null &&
                prefab.GetComponent<MeshRenderer>() != null && prefab.GetComponent<MeshFilter>() != null &&
                prefab.GetComponents<Component>().Length == 5 && prefab.transform.childCount == 0,
                "The console prefab differs from the inspected single primitive with ShutdownConsole. It was not modified.");
            CheckConsoleDefaults(prefab.GetComponent<ShutdownConsole>());
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/EnvironmentVisualsV1/Core-Accent.mat");
            if (material == null) material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/WorldGreybox/CoreCyan.mat");
            Require(material != null && material.shader != null && material.shader.name == "Universal Render Pipeline/Lit", "An existing URP Core accent material is required; no material will be created.");
            Transform existing = UniqueChild(parent, RootName, false);
            List<GameFlowController> controllers = Components<GameFlowController>(scene);
            List<ShutdownConsole> consoles = Components<ShutdownConsole>(scene);
            if (existing != null)
            {
                Require(controllers.Count == 1 && controllers[0].transform == existing && consoles.Count == 1, "Duplicate or foreign game-flow controllers/consoles were preserved.");
                ValidateExisting(existing, player, consoleMarker, exitMarker, prefab, material);
                Debug.Log("Setup Game Flow: already configured. No scene, object or asset changed.");
                return;
            }
            Require(controllers.Count == 0 && consoles.Count == 0, "An existing game-flow controller or authored console was found. Nothing was overwritten.");
            Require(!scene.isDirty, "Save Main's existing edits before the first setup.");
            var originals = new Dictionary<UnityEngine.Object, string>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    originals.Add(item.gameObject, EditorJsonUtility.ToJson(item.gameObject));
                    foreach (Component component in item.GetComponents<Component>())
                    {
                        Require(component != null, "A missing script exists in Main. Resolve it before setup.");
                        if (component != parent) originals.Add(component, EditorJsonUtility.ToJson(component));
                    }
                }
            var baked = new Dictionary<string, string>();
            foreach (NavMeshSurface surface in Components<NavMeshSurface>(scene))
            {
                string path = AssetDatabase.GetAssetPath(surface.navMeshData);
                if (string.IsNullOrEmpty(path)) continue;
                baked[path] = FileHash(path);
                baked[path + ".meta"] = FileHash(path + ".meta");
            }
            Require(baked.Count > 0, "No saved referenced NavMesh data was found. Setup will not bake or repair navigation.");
            Vector3 parentPosition = parent.position;
            Quaternion parentRotation = parent.rotation;
            Vector3 parentScale = parent.lossyScale;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Blackout Protocol Game Flow");
            try
            {
                var root = new GameObject(RootName);
                root.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(root, RootName);
                GameObject consoleObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                Undo.RegisterCreatedObjectUndo(consoleObject, RootName);
                consoleObject.name = "Shutdown Console";
                consoleObject.transform.SetPositionAndRotation(consoleMarker.position + Vector3.up * 1.2f, Quaternion.identity);
                consoleObject.transform.localScale = ConsoleScale;
                consoleObject.layer = 8;
                consoleObject.isStatic = false;
                consoleObject.GetComponent<BoxCollider>().isTrigger = true;
                consoleObject.GetComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.RecordPrefabInstancePropertyModifications(consoleObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(consoleObject.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(consoleObject.GetComponent<BoxCollider>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(consoleObject.GetComponent<MeshRenderer>());
                var sign = new GameObject("Elevator Exit Sign");
                sign.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(sign, RootName);
                sign.layer = 2;
                sign.transform.SetPositionAndRotation(exitMarker.position + LabelOffset, Quaternion.Euler(0f, 90f, 0f));
                TextMesh label = Undo.AddComponent<TextMesh>(sign);
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
                label.text = "ELEVATOR EXIT\nLOCKED";
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.fontSize = 40;
                label.characterSize = 0.06f;
                label.color = new Color(1f, 0.55f, 0.15f);
                GameFlowController controller = Undo.AddComponent<GameFlowController>(root);
                Undo.AddComponent<GameFlowHud>(root);
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("player").objectReferenceValue = player;
                serialized.FindProperty("console").objectReferenceValue = consoleObject.GetComponent<ShutdownConsole>();
                serialized.FindProperty("consoleMarker").objectReferenceValue = consoleMarker;
                serialized.FindProperty("exitMarker").objectReferenceValue = exitMarker;
                serialized.FindProperty("exitLabel").objectReferenceValue = label;
                serialized.FindProperty("setupVersion").stringValue = RootName;
                serialized.ApplyModifiedProperties();
                ValidateExisting(root.transform, player, consoleMarker, exitMarker, prefab, material);
                foreach (KeyValuePair<UnityEngine.Object, string> entry in originals)
                    Require(entry.Key != null && EditorJsonUtility.ToJson(entry.Key) == entry.Value, "A preexisting scene object changed. Setup was rolled back.");
                Require(parent.position == parentPosition && parent.rotation == parentRotation && parent.lossyScale == parentScale, "The marker parent transform changed. Setup was rolled back.");
                foreach (KeyValuePair<string, string> entry in baked)
                    Require(FileHash(entry.Key) == entry.Value, "A baked file changed externally. Scene setup was rolled back.");
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("Setup Game Flow: added a trigger-only layer-8 console, logical extraction, sign and flow/HUD components. Existing scene objects and baked navigation were preserved. Review, save Main, then Play. R/Start restarts after Win/Lose. No assets were created or rebaked.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static void ValidateExisting(Transform root, PlayerState player, Transform consoleMarker, Transform exitMarker, GameObject prefab, Material material)
        {
            Require(root.localPosition == Vector3.zero && root.localRotation == Quaternion.identity && root.localScale == Vector3.one &&
                root.gameObject.activeInHierarchy && root.childCount == 2 && root.GetComponents<Component>().Length == 3 &&
                root.GetComponent<GameFlowController>() != null && root.GetComponent<GameFlowHud>() != null &&
                root.GetComponent<GameFlowController>().enabled && root.GetComponent<GameFlowHud>().enabled, "The owned flow root contains unexpected authored state.");
            Transform console = UniqueChild(root, "Shutdown Console", true);
            Transform sign = UniqueChild(root, "Elevator Exit Sign", true);
            Require(PrefabUtility.GetCorrespondingObjectFromSource(console.gameObject) == prefab && console.childCount == 0 &&
                console.GetComponents<Component>().Length == 5 && console.gameObject.activeInHierarchy && !console.gameObject.isStatic &&
                console.position == consoleMarker.position + Vector3.up * 1.2f && console.rotation == Quaternion.identity && console.localScale == ConsoleScale &&
                console.gameObject.layer == 8, "The console instance was authored or replaced. It was preserved.");
            BoxCollider collider = console.GetComponent<BoxCollider>();
            Require(collider != null && collider.enabled && collider.isTrigger && collider.center == Vector3.zero && collider.size == Vector3.one &&
                console.GetComponent<MeshFilter>() != null && console.GetComponent<MeshFilter>().sharedMesh != null &&
                console.GetComponent<MeshFilter>().sharedMesh == prefab.GetComponent<MeshFilter>().sharedMesh &&
                console.GetComponent<ShutdownConsole>() != null && console.GetComponent<ShutdownConsole>().enabled &&
                console.GetComponent<MeshRenderer>() != null && console.GetComponent<MeshRenderer>().enabled &&
                console.GetComponent<MeshRenderer>().sharedMaterials.Length == 1 && console.GetComponent<MeshRenderer>().sharedMaterial == material,
                "The console collider, component or material differs from this setup. Nothing was overwritten.");
            TextMesh label = sign.GetComponent<TextMesh>();
            Require(label != null, "Exit sign: missing TextMesh component.");
            Require(sign.childCount == 0, "Exit sign: unexpected child objects.");
            Require(sign.GetComponents<Component>().Length == 3, "Exit sign: unexpected component count; expected Transform, TextMesh and MeshRenderer.");
            Require(sign.gameObject.activeInHierarchy, "Exit sign: inactive in hierarchy.");
            Require(sign.gameObject.layer == 2, "Exit sign: layer differs; expected Ignore Raycast (2).");
            Require(sign.position == exitMarker.position + LabelOffset, "Exit sign: world position differs from the elevator marker plus LabelOffset.");
            Require(sign.rotation == Quaternion.Euler(0f, 90f, 0f), "Exit sign: world rotation differs; expected (0, 90, 0).");
            Require(sign.localScale == Vector3.one, "Exit sign: local scale differs; expected (1, 1, 1).");
            Require(label.text == "ELEVATOR EXIT\nLOCKED", "Exit sign: text differs; expected ELEVATOR EXIT / LOCKED.");
            Require(label.fontSize == 40, "Exit sign: font size differs; expected 40.");
            Require(Mathf.Approximately(label.characterSize, 0.06f), "Exit sign: character size differs; expected 0.06.");
            Require(label.anchor == TextAnchor.MiddleCenter, "Exit sign: anchor differs; expected MiddleCenter.");
            Require(label.alignment == TextAlignment.Center, "Exit sign: alignment differs; expected Center.");
            Require(label.font == Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), "Exit sign: font differs; expected LegacyRuntime.ttf.");
            Color32 actualColor = label.color;
            Color32 expectedColor = new Color(1f, 0.55f, 0.15f);
            Require(actualColor.r == expectedColor.r && actualColor.g == expectedColor.g && actualColor.b == expectedColor.b && actualColor.a == expectedColor.a,
                $"Exit sign: Color32 color differs; actual {actualColor}, expected {expectedColor}.");
            MeshRenderer signRenderer = sign.GetComponent<MeshRenderer>();
            Require(signRenderer != null, "Exit sign: missing MeshRenderer component.");
            Require(signRenderer.enabled, "Exit sign: MeshRenderer is disabled.");
            Require(signRenderer.sharedMaterial == label.font.material, "Exit sign: shared material differs from the font material.");
            CheckConsoleDefaults(console.GetComponent<ShutdownConsole>());
            var serialized = new SerializedObject(root.GetComponent<GameFlowController>());
            Require(serialized.FindProperty("setupVersion").stringValue == RootName &&
                serialized.FindProperty("player").objectReferenceValue == player &&
                serialized.FindProperty("console").objectReferenceValue == console.GetComponent<ShutdownConsole>() &&
                serialized.FindProperty("consoleMarker").objectReferenceValue == consoleMarker &&
                serialized.FindProperty("exitMarker").objectReferenceValue == exitMarker &&
                serialized.FindProperty("exitLabel").objectReferenceValue == label, "Game-flow references were changed. They were preserved.");
        }

        private static void CheckConsoleDefaults(ShutdownConsole console)
        {
            var serialized = new SerializedObject(console);
            Require(serialized.FindProperty("indicatorRenderer").objectReferenceValue == console.GetComponent<MeshRenderer>() &&
                serialized.FindProperty("readyColour").colorValue == new Color(0f, 0.7f, 1f) &&
                serialized.FindProperty("activatedColour").colorValue == Color.green &&
                serialized.FindProperty("audioSource").objectReferenceValue == null &&
                serialized.FindProperty("activationClip").objectReferenceValue == null &&
                serialized.FindProperty("onShutdownActivated.m_PersistentCalls.m_Calls").arraySize == 0,
                "The console contains authored feedback/audio/events. Those references were preserved; nothing was wired or overwritten.");
        }

        private static void CheckMarker(Transform marker, Vector3 expected)
        {
            Require(marker.position == expected && marker.rotation == Quaternion.identity && marker.lossyScale == Vector3.one &&
                marker.GetComponents<Component>().Length == 1 && marker.childCount == 0 && marker.gameObject.activeInHierarchy,
                "The expected empty marker was moved, replaced or authored: " + marker.name);
        }

        private static Transform FindPath(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            Transform found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == parts[0]) { Require(found == null, "Duplicate Environment root."); found = root.transform; }
            Require(found != null, "Missing Environment root.");
            for (int index = 1; index < parts.Length; index++) found = UniqueChild(found, parts[index], true);
            return found;
        }

        private static Transform UniqueChild(Transform parent, string name, bool required)
        {
            Transform found = null;
            foreach (Transform child in parent)
                if (child.name == name) { Require(found == null, "Duplicate child: " + name); found = child; }
            Require(found != null || !required, "Missing child: " + name);
            return found;
        }

        private static List<TComponent> Components<TComponent>(Scene scene) where TComponent : Component
        {
            var result = new List<TComponent>();
            foreach (GameObject root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<TComponent>(true));
            return result;
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string FileHash(string path) { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))); }
    }
}
