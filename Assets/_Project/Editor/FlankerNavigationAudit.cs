using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace BlackoutProtocol.Editor
{
    /// <summary>Queries Main's loaded navigation without changing scenes, surfaces or assets.</summary>
    public static class FlankerNavigationAudit
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string Prefix = "[Navigation Audit] ";
        private const float SampleRadius = 0.5f;
        private const float MaximumHeightError = 0.25f;

        private readonly struct Crossing
        {
            public Crossing(string name, Vector3 start, Vector3 end)
            {
                Name = name;
                Start = start;
                End = end;
            }

            public string Name { get; }
            public Vector3 Start { get; }
            public Vector3 End { get; }
        }

        /// <summary>Reports baked surface references, complete zone paths and direct corridor crossings.</summary>
        [MenuItem("Tools/Blackout Protocol/Audit Navigation")]
        public static void Audit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError(Prefix + "FAIL: Exit Play Mode and Prefab Mode. This command audits the loaded Main scene in Edit Mode only.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded || SceneManager.sceneCount != 1)
            {
                Debug.LogError(Prefix + "FAIL: Open Main.unity alone first. No scenes were opened or closed. Other loaded scenes could mask broken routes.");
                return;
            }

            int humanoid = FindHumanoid();
            if (humanoid < 0)
            {
                Debug.LogError(Prefix + "FAIL: No agent type named Humanoid exists in Navigation settings.");
                return;
            }

            int failures = 0;
            int warnings = 0;
            if (scene.isDirty)
                Warn("Main has unsaved changes. Results describe its current loaded state, not necessarily the saved scene.", ref warnings);

            int bakedHumanoidSurfaces = InspectSurfaces(scene, humanoid, ref warnings);
            if (bakedHumanoidSurfaces == 0)
            {
                Debug.LogError(Prefix + "FAIL: Main has no active, enabled Humanoid surface with baked data. Nothing was baked or registered by this audit.");
                return;
            }

            var filter = new NavMeshQueryFilter { agentTypeID = humanoid, areaMask = NavMesh.AllAreas };
            string[] names = { "Loading Bay", "Security Hub", "Maintenance", "Control Core" };
            Vector3[] anchors =
            {
                new Vector3(-30f, 0f, 0f),
                new Vector3(0f, 0f, 0f),
                new Vector3(12f, 0f, 30f),
                new Vector3(28f, 0f, 0f)
            };
            var sampled = new Vector3[anchors.Length];
            var valid = new bool[anchors.Length];
            for (int index = 0; index < anchors.Length; index++)
                valid[index] = Sample(names[index], anchors[index], filter, out sampled[index], ref failures);

            var path = new NavMeshPath();
            int completePaths = 0;
            for (int start = 0; start < anchors.Length; start++)
            {
                for (int end = 0; end < anchors.Length; end++)
                {
                    if (start == end) continue;
                    string label = names[start] + " -> " + names[end];
                    if (!valid[start] || !valid[end])
                        Fail(label + ": cannot test path because an anchor failed sampling.", ref failures);
                    else if (CompletePath(label, sampled[start], sampled[end], filter, path, ref failures))
                        completePaths++;
                }
            }

            Crossing[] crossings =
            {
                new Crossing("Loading Bay / Hub", new Vector3(-15f, 0f, 0f), new Vector3(-11f, 0f, 0f)),
                new Crossing("Hub / Core DIRECT", new Vector3(10f, 0f, 0f), new Vector3(22f, 0f, 0f)),
                new Crossing("Hub / Maintenance", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 24f)),
                new Crossing("Maintenance / Core", new Vector3(26f, 0f, 24f), new Vector3(26f, 0f, 10f))
            };
            int clearCrossings = 0;
            foreach (Crossing crossing in crossings)
            {
                bool startValid = Sample(crossing.Name + " start", crossing.Start, filter, out Vector3 start, ref failures);
                bool endValid = Sample(crossing.Name + " end", crossing.End, filter, out Vector3 end, ref failures);
                if (!startValid || !endValid) continue;

                bool forwardPath = CompletePath(crossing.Name + " forward", start, end, filter, path, ref failures);
                bool reversePath = CompletePath(crossing.Name + " reverse", end, start, filter, path, ref failures);
                bool forwardClear = ClearCrossing(crossing.Name + " forward", start, end, filter, ref failures);
                bool reverseClear = ClearCrossing(crossing.Name + " reverse", end, start, filter, ref failures);
                if (forwardPath && reversePath && forwardClear && reverseClear) clearCrossings++;
            }

            string summary = $"Zone paths: {completePaths}/12 complete; corridors: {clearCrossings}/4 clear both ways; failures: {failures}; warnings: {warnings}. No scene, surface or asset was modified. Queries inspect currently registered Humanoid data, not asset existence alone.";
            if (failures > 0) Debug.LogError(Prefix + "FAIL: " + summary);
            else if (warnings > 0) Debug.LogWarning(Prefix + "PASS WITH WARNINGS: " + summary);
            else Debug.Log(Prefix + "PASS: " + summary);
        }

        private static int FindHumanoid()
        {
            for (int index = 0; index < NavMesh.GetSettingsCount(); index++)
            {
                int agent = NavMesh.GetSettingsByIndex(index).agentTypeID;
                if (string.Equals(NavMesh.GetSettingsNameFromID(agent), "Humanoid", StringComparison.Ordinal))
                    return agent;
            }
            return -1;
        }

        private static int InspectSurfaces(Scene scene, int humanoid, ref int warnings)
        {
            var enabledByAgent = new Dictionary<int, int>();
            int surfaceCount = 0;
            int bakedHumanoidSurfaces = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NavMeshSurface surface in root.GetComponentsInChildren<NavMeshSurface>(true))
                {
                    surfaceCount++;
                    bool enabled = surface.isActiveAndEnabled;
                    NavMeshData data = surface.navMeshData;
                    string assetPath = data != null ? AssetDatabase.GetAssetPath(data) : "<none>";
                    string label = HierarchyPath(surface.transform);
                    Debug.Log(Prefix + $"Surface {label}: enabled={surface.enabled}, activeInHierarchy={surface.gameObject.activeInHierarchy}, agent={surface.agentTypeID}, bakedData={assetPath}, collect={surface.collectObjects}, geometry={surface.useGeometry}, layerMask=0x{surface.layerMask.value:X8}, worldOrigin={surface.transform.position}.", surface);
                    if (!enabled) continue;

                    enabledByAgent.TryGetValue(surface.agentTypeID, out int count);
                    enabledByAgent[surface.agentTypeID] = count + 1;
                    if (data == null)
                        Warn(label + ": enabled surface has no baked data. This is not proof of a disconnected route.", ref warnings);
                    else
                    {
                        if (surface.agentTypeID == humanoid) bakedHumanoidSurfaces++;
                        if (string.IsNullOrEmpty(assetPath))
                            Warn(label + ": baked data exists in memory but is not a saved asset.", ref warnings);
                    }
                }
            }

            foreach (KeyValuePair<int, int> pair in enabledByAgent)
            {
                if (pair.Value > 1)
                    Warn($"Duplicate enabled surfaces: agent {pair.Key} has {pair.Value}. Queries can combine their registered meshes; inspect overlap before trusting surface-specific results. Nothing was disabled.", ref warnings);
            }
            Debug.Log(Prefix + $"Found {surfaceCount} surface(s); {bakedHumanoidSurfaces} enabled Humanoid surface(s) reference baked data.");
            return bakedHumanoidSurfaces;
        }

        private static bool Sample(string label, Vector3 requested, NavMeshQueryFilter filter, out Vector3 sampled, ref int failures)
        {
            sampled = requested;
            if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, SampleRadius, filter))
            {
                Fail($"{label}: no Humanoid NavMesh within {SampleRadius} m of {requested}.", ref failures);
                return false;
            }
            if (Mathf.Abs(hit.position.y - requested.y) > MaximumHeightError)
            {
                Fail($"{label}: sampled {hit.position}, exceeding {MaximumHeightError} m height tolerance from {requested}. Check floor/bake elevation.", ref failures);
                return false;
            }
            sampled = hit.position;
            Debug.Log(Prefix + $"Sample {label}: {requested} -> {sampled}.");
            return true;
        }

        private static bool CompletePath(string label, Vector3 start, Vector3 end, NavMeshQueryFilter filter, NavMeshPath path, ref int failures)
        {
            path.ClearCorners();
            bool found = NavMesh.CalculatePath(start, end, filter, path);
            if (!found || path.status != NavMeshPathStatus.PathComplete)
            {
                Fail($"{label}: CalculatePath={found}, status={path.status}; required PathComplete. Start={start}, end={end}.", ref failures);
                return false;
            }
            Debug.Log(Prefix + "PASS PathComplete: " + label);
            return true;
        }

        private static bool ClearCrossing(string label, Vector3 start, Vector3 end, NavMeshQueryFilter filter, ref int failures)
        {
            if (NavMesh.Raycast(start, end, out NavMeshHit hit, filter))
            {
                Fail($"{label}: direct corridor crossing blocked at {hit.position}. A complete path elsewhere could be a detour.", ref failures);
                return false;
            }
            Debug.Log(Prefix + "PASS direct corridor crossing: " + label);
            return true;
        }

        private static string HierarchyPath(Transform current)
        {
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }

        private static void Warn(string message, ref int warnings)
        {
            warnings++;
            Debug.LogWarning(Prefix + "WARNING: " + message);
        }

        private static void Fail(string message, ref int failures)
        {
            failures++;
            Debug.LogError(Prefix + "FAIL: " + message);
        }
    }
}
