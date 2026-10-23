using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BlackoutProtocol.Core;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BlackoutProtocol.Editor
{
    /// <summary>Applies a repeatable visual treatment to the approved greybox without changing its geometry or gameplay.</summary>
    public static class EnvironmentVisuals
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string Folder = "Assets/_Project/Materials/EnvironmentVisualsV1";
        private const string ManifestPath = Folder + "/assets.json";
        private const string Version = "Blackout Environment Visuals v1";
        private const string GeometryHash = "6a6a46da3ea68252ed4ed4f902957feb0b4acaff37380c58314710b43148f462";
        private const string GeneratedRoot = "Blackout Protocol - Generated Greybox v1";
        private const string OldMaterials = "Assets/_Project/Materials/WorldGreybox/";
        private const string LightingRevision = Version + " lighting correction";
        private const float SunIntensity = 0.65f;
        private static readonly Color Ambient = new Color(0.65f, 0.69f, 0.75f);
        private static readonly Color SunColor = new Color(0.82f, 0.88f, 1f);
        private static readonly Color LegacyAmbient = new Color(0.32f, 0.35f, 0.4f);
        private static readonly Color LegacySunColor = new Color(0.56f, 0.67f, 0.8f);

        [Serializable]
        private sealed class Stamp
        {
            public string file;
            public string hash;
        }

        [Serializable]
        private sealed class Manifest
        {
            public string version;
            public Stamp[] assets;
        }

        private sealed class Theme
        {
            public string Key;
            public Color Floor;
            public Color Wall;
            public Color Cover;
            public Color Accent;
            public Color OriginalAccent;
            public float Intensity;
            public float Range;
        }

        private sealed class Scope
        {
            public string Path;
            public string Key;
            public int Theme;
            public Vector2 Size;
            public int Walls;
            public int Covers;
            public string Label;
            public bool Reactor;
            public Transform Transform;
            public Light Light;
        }

        private sealed class Target
        {
            public MeshRenderer Renderer;
            public string Material;
            public string Original;
        }

        private sealed class Plan
        {
            public Scope[] Scopes;
            public readonly List<Target> Targets = new List<Target>();
            public Light Sun;
        }

        private static readonly Theme[] Themes =
        {
            new Theme { Key = "Loading", Floor = new Color(0.25f, 0.26f, 0.27f), Wall = new Color(0.34f, 0.32f, 0.29f),
                Cover = new Color(0.8f, 0.66f, 0.43f), Accent = new Color(1f, 0.12f, 0.035f), OriginalAccent = new Color(0.65f, 0.14f, 0.12f), Intensity = 3f, Range = 15f },
            new Theme { Key = "Security", Floor = new Color(0.2f, 0.25f, 0.29f), Wall = new Color(0.29f, 0.38f, 0.46f),
                Cover = new Color(0.63f, 0.74f, 0.82f), Accent = new Color(0.08f, 0.65f, 1f), OriginalAccent = new Color(0.12f, 0.38f, 0.7f), Intensity = 2.5f, Range = 17f },
            new Theme { Key = "Maintenance", Floor = new Color(0.36f, 0.31f, 0.23f), Wall = new Color(0.38f, 0.36f, 0.28f),
                Cover = new Color(0.8f, 0.6f, 0.36f), Accent = new Color(1f, 0.52f, 0.06f), OriginalAccent = new Color(0.8f, 0.55f, 0.1f), Intensity = 2.8f, Range = 18f },
            new Theme { Key = "Core", Floor = new Color(0.2f, 0.3f, 0.3f), Wall = new Color(0.53f, 0.63f, 0.62f),
                Cover = new Color(0.72f, 0.8f, 0.74f), Accent = new Color(0.07f, 1f, 0.75f), OriginalAccent = new Color(0.08f, 0.7f, 0.7f), Intensity = 3.5f, Range = 18f }
        };

        /// <summary>Validates Main and owned assets before applying material and lighting overrides only.</summary>
        [MenuItem("Tools/Blackout Protocol/Apply Environment Visuals")]
        public static void Apply()
        {
            try { ApplyChecked(); }
            catch (Exception exception) { Debug.LogError(Version + " stopped: " + exception.Message); }
        }

        private static void ApplyChecked()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Exit Play Mode and Prefab Mode first.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded || SceneManager.sceneCount != 1 || SceneManager.GetActiveScene() != scene)
                throw new InvalidOperationException("Open Main.unity alone and make it active. No scene was opened or switched.");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                throw new InvalidOperationException("The active quality/graphics pipeline must be URP. Pipeline assets are never changed.");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader sky = Shader.Find("Skybox/Procedural");
            if (lit == null || sky == null) throw new InvalidOperationException("Required URP Lit or procedural sky shader is unavailable.");

            Plan plan = Inspect(scene);
            int styled = 0;
            foreach (Target target in plan.Targets)
            {
                string path = AssetDatabase.GetAssetPath(target.Renderer.sharedMaterial);
                if (path == Folder + "/" + target.Material + ".mat") styled++;
                else if (path != OldMaterials + target.Original + ".mat")
                    throw new InvalidOperationException("Unexpected authored material on " + Hierarchy(target.Renderer.transform) + ". It was preserved.");
            }
            if (styled != 0 && styled != plan.Targets.Count)
                throw new InvalidOperationException("Environment materials are partially styled or manually mixed. Refusing to overwrite them.");

            bool upgrading = false;
            Dictionary<string, Material> palette = null;
            if (Directory.Exists(Folder))
            {
                ValidateAssets(plan);
                palette = LoadPalette(plan);
                if (styled == plan.Targets.Count)
                {
                    if (MatchesLighting(plan, palette))
                    {
                        Debug.Log(LightingRevision + ": already applied. No assets, objects or scene state changed.");
                        return;
                    }
                    if (!MatchesLighting(plan, palette, true))
                        throw new InvalidOperationException("Lighting was edited after the visual pass. Preserve those edits; nothing was reapplied.");
                    upgrading = true;
                }
            }
            else if (File.Exists(Folder)) throw new InvalidOperationException("The reserved asset folder path is occupied.");
            if (scene.isDirty) throw new InvalidOperationException("Save Main's pending edits before applying or upgrading visuals. No scene changes were made.");
            if (!upgrading) CheckOriginalLighting(plan);

            string before = ProtectedState(scene);
            Dictionary<string, string> baked = BakedAssetHashes(scene);
            if (palette == null) palette = CreatePalette(plan, lit, sky);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(LightingRevision);
            try
            {
                var objects = new List<UnityEngine.Object> { plan.Sun, Unsupported.GetRenderSettings() };
                foreach (Target target in plan.Targets) objects.Add(target.Renderer);
                foreach (Scope scope in plan.Scopes) if (scope.Light != null) objects.Add(scope.Light);
                Undo.RecordObjects(objects.ToArray(), LightingRevision);
                if (!upgrading)
                    foreach (Target target in plan.Targets) target.Renderer.sharedMaterial = palette[target.Material];
                foreach (Scope scope in plan.Scopes)
                {
                    if (scope.Light == null) continue;
                    Theme theme = Themes[scope.Theme];
                    scope.Light.color = theme.Accent;
                    scope.Light.intensity = theme.Intensity;
                    scope.Light.range = theme.Range;
                }
                plan.Sun.color = SunColor;
                plan.Sun.intensity = SunIntensity;
                RenderSettings.skybox = palette["Night-Sky"];
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Ambient;
                RenderSettings.reflectionIntensity = 0.35f;
                Undo.FlushUndoRecordObjects();
                if (ProtectedState(scene) != before) throw new InvalidOperationException("A protected object changed during the pass. Scene overrides were rolled back.");
                foreach (KeyValuePair<string, string> entry in baked)
                    if (FileHash(entry.Key) != entry.Value) throw new InvalidOperationException("A baked navigation file changed externally during the pass. Scene overrides were rolled back.");
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log(LightingRevision + (upgrading ? ": upgraded v1 global fill; existing materials and zone lights retained. " : ": applied environment visuals with corrected global fill. ") +
                    "Geometry, actors, cover, navigation and baked data were preserved. Review the result, then save Main with Cmd+S. No bake or camera/post-processing changes were made.");
            }
            catch
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static Plan Inspect(Scene scene)
        {
            var roots = new Dictionary<string, Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (IsActor(root)) continue;
                if (roots.ContainsKey(root.name) || (root.name != "Environment" && root.name != "Main Camera" && root.name != "Directional Light"))
                    throw new InvalidOperationException("Unexpected scene root: " + root.name);
                roots.Add(root.name, root.transform);
            }
            if (roots.Count != 3) throw new InvalidOperationException("Expected Environment, Main Camera and Directional Light roots, plus optional actors.");
            Transform environment = roots["Environment"];
            Transform generated = Child(environment, GeneratedRoot);
            Transform geometry = Child(generated, "Navigation Geometry");
            CheckChildren(environment, "01_LoadingBay", GeneratedRoot);
            CheckChildren(generated, "Navigation Geometry", "Spawn Markers - no AI");
            CheckChildren(geometry, "02 Security Hub", "03 Maintenance Storage", "04 Control Core", "Loading to Hub", "Hub to Core - Direct", "Hub to Maintenance", "Maintenance to Core");
            Scope[] scopes =
            {
                new Scope { Path = "01_LoadingBay", Key = "LoadingBay", Theme = 0, Size = new Vector2(20f, 18f), Walls = 5, Covers = 3, Label = "01  LOADING BAY" },
                new Scope { Path = GeneratedRoot + "/Navigation Geometry/02 Security Hub", Key = "SecurityHub", Theme = 1, Size = new Vector2(24f, 24f), Walls = 7, Covers = 6, Label = "02  SECURITY HUB" },
                new Scope { Path = GeneratedRoot + "/Navigation Geometry/03 Maintenance Storage", Key = "Maintenance", Theme = 2, Size = new Vector2(32f, 16f), Walls = 5, Covers = 3, Label = "03  MAINTENANCE / STORAGE" },
                new Scope { Path = GeneratedRoot + "/Navigation Geometry/04 Control Core", Key = "ControlCore", Theme = 3, Size = new Vector2(20f, 24f), Walls = 6, Covers = 3, Label = "04  CONTROL CORE", Reactor = true },
                new Scope { Path = GeneratedRoot + "/Navigation Geometry/Loading to Hub", Key = "Loading-Hub", Theme = 0, Size = new Vector2(2f, 4f), Walls = 2 },
                new Scope { Path = GeneratedRoot + "/Navigation Geometry/Hub to Core - Direct", Key = "Hub-Core", Theme = 1, Size = new Vector2(8f, 4f), Walls = 2 },
                new Scope { Path = GeneratedRoot + "/Navigation Geometry/Hub to Maintenance", Key = "Hub-Maintenance", Theme = 2, Size = new Vector2(4f, 10f), Walls = 2 },
                new Scope { Path = GeneratedRoot + "/Navigation Geometry/Maintenance to Core", Key = "Maintenance-Core", Theme = 3, Size = new Vector2(4f, 10f), Walls = 2 }
            };
            var plan = new Plan { Scopes = scopes, Sun = roots["Directional Light"].GetComponent<Light>() };
            if (plan.Sun == null || plan.Sun.type != LightType.Directional || !plan.Sun.isActiveAndEnabled)
                throw new InvalidOperationException("The original active Directional Light is required.");
            var rows = new List<string>();
            foreach (Scope scope in scopes)
            {
                scope.Transform = environment.Find(scope.Path);
                if (scope.Transform == null || scope.Transform.GetComponents<Component>().Length != 1 || PrefabUtility.IsPartOfPrefabInstance(scope.Transform))
                    throw new InvalidOperationException("Missing or authored zone/corridor container: " + scope.Path);
                var counts = new Dictionary<string, int>();
                foreach (Transform child in scope.Transform)
                {
                    if (IsActor(child.gameObject)) continue;
                    string role = child.name;
                    if (role.StartsWith("Cover Point ", StringComparison.Ordinal))
                    {
                        if (child.GetComponent<CoverPoint>() == null || child.GetComponents<Component>().Length != 2 || child.childCount != 0)
                            throw new InvalidOperationException("Unexpected cover marker content: " + Hierarchy(child));
                        Count(counts, "CoverPoint");
                        continue;
                    }
                    if (role == scope.Label)
                    {
                        if (child.GetComponent<TextMesh>() == null || child.childCount != 0)
                            throw new InvalidOperationException("Unexpected zone label content: " + Hierarchy(child));
                        Count(counts, "Label");
                        continue;
                    }
                    if (role == "Zone Light")
                    {
                        scope.Light = child.GetComponent<Light>();
                        if (scope.Label == null || scope.Light == null || scope.Light.type != LightType.Point || !scope.Light.isActiveAndEnabled ||
                            scope.Light.lightmapBakeType != LightmapBakeType.Realtime || child.childCount != 0 || child.GetComponents<Component>().Length != 3 ||
                            child.GetComponent<UniversalAdditionalLightData>() == null)
                            throw new InvalidOperationException("Unexpected zone light: " + Hierarchy(child));
                        Count(counts, "Light");
                        continue;
                    }
                    bool cover = role.StartsWith("Cover Block ", StringComparison.Ordinal);
                    if (role != "Floor" && role != "Wall" && role != "Zone Identification Stripe" && role != "Power Core Landmark" && !cover)
                        throw new InvalidOperationException("Unexpected authored object in a visual target scope: " + Hierarchy(child));
                    BoxCollider collider = child.GetComponent<BoxCollider>();
                    MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                    MeshFilter filter = child.GetComponent<MeshFilter>();
                    int expectedLayer = role == "Floor" || role == "Zone Identification Stripe" ? GameLayers.Ground : GameLayers.Obstacle;
                    if (collider == null || renderer == null || filter == null || filter.sharedMesh == null || filter.sharedMesh.name != "Cube" ||
                        !collider.enabled || collider.isTrigger || collider.center != Vector3.zero || collider.size != Vector3.one ||
                        !child.gameObject.activeInHierarchy || !renderer.enabled || child.gameObject.layer != expectedLayer || child.childCount != 0 ||
                        child.GetComponents<Component>().Length != 4 || renderer.sharedMaterials.Length != 1 || PrefabUtility.IsPartOfPrefabInstance(child))
                        throw new InvalidOperationException("Unexpected mesh, collider, layer or authored component on " + Hierarchy(child));
                    string category = cover ? "Cover" : role;
                    Count(counts, category);
                    string key = role == "Floor" ? scope.Key + "-Floor" : role == "Wall" ? Themes[scope.Theme].Key + "-Wall" :
                        cover ? Themes[scope.Theme].Key + "-Cover" : role == "Power Core Landmark" ? "Core-Reactor" : Themes[scope.Theme].Key + "-Accent";
                    string original = role == "Floor" ? "Floor" : role == "Wall" ? "Wall" : cover ? "Cover" :
                        scope.Theme == 0 ? "LoadingRed" : scope.Theme == 1 ? "SecurityBlue" : scope.Theme == 2 ? "MaintenanceAmber" : "CoreCyan";
                    plan.Targets.Add(new Target { Renderer = renderer, Material = key, Original = original });
                    rows.Add(Hierarchy(child) + "|" + Format(child.position) + "|" + Format(child.lossyScale) + "|" +
                        Number(child.rotation.x) + "," + Number(child.rotation.y) + "," + Number(child.rotation.z) + "," + Number(child.rotation.w) + "|" + expectedLayer);
                }
                RequireCount(counts, "Floor", 1);
                RequireCount(counts, "Wall", scope.Walls);
                RequireCount(counts, "Cover", scope.Covers);
                RequireCount(counts, "CoverPoint", scope.Covers);
                RequireCount(counts, "Zone Identification Stripe", scope.Label == null ? 0 : 1);
                RequireCount(counts, "Label", scope.Label == null ? 0 : 1);
                RequireCount(counts, "Light", scope.Label == null ? 0 : 1);
                RequireCount(counts, "Power Core Landmark", scope.Reactor ? 1 : 0);
            }
            rows.Sort(StringComparer.Ordinal);
            if (plan.Targets.Count != 59 || Hash(string.Join("\n", rows)) != GeometryHash)
                throw new InvalidOperationException("Environment geometry differs from the inspected 59-box layout. No transforms or colliders were changed; review the layout before updating this visual recipe.");
            return plan;
        }

        private static Dictionary<string, Material> CreatePalette(Plan plan, Shader lit, Shader sky)
        {
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "EnvironmentVisualsV1");
            Texture2D panels = TextureAsset("PanelGrid", 0);
            Texture2D grating = TextureAsset("Grating", 1);
            Texture2D cargo = TextureAsset("Cargo", 2);
            foreach (Scope scope in plan.Scopes)
                Lit(scope.Key + "-Floor", lit, Themes[scope.Theme].Floor, scope.Theme == 2 ? grating : panels, scope.Size * 0.5f, 0.55f, 0.32f, Color.black);
            foreach (Theme theme in Themes)
            {
                Lit(theme.Key + "-Wall", lit, theme.Wall, panels, new Vector2(3f, 1f), 0.4f, 0.28f, Color.black);
                Lit(theme.Key + "-Cover", lit, theme.Cover, cargo, Vector2.one, 0.45f, 0.24f, Color.black);
                Lit(theme.Key + "-Accent", lit, theme.Accent * 0.35f, null, Vector2.one, 0.3f, 0.4f, theme.Accent * 2.3f);
            }
            Lit("Core-Reactor", lit, Themes[3].Accent * 0.25f, panels, new Vector2(2f, 3f), 0.5f, 0.5f, Themes[3].Accent * 3f);
            var night = new Material(sky) { name = "Night-Sky" };
            night.SetColor("_SkyTint", new Color(0.04f, 0.07f, 0.1f));
            night.SetColor("_GroundColor", new Color(0.015f, 0.02f, 0.03f));
            night.SetFloat("_AtmosphereThickness", 0.2f);
            night.SetFloat("_Exposure", 0.25f);
            night.SetFloat("_SunDisk", 0f);
            AssetDatabase.CreateAsset(night, Folder + "/Night-Sky.mat");
            foreach (string name in AssetNames(plan))
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(Folder + "/" + name));
            var stamps = new List<Stamp>();
            foreach (string name in AssetNames(plan))
            {
                stamps.Add(new Stamp { file = name, hash = FileHash(Folder + "/" + name) });
                stamps.Add(new Stamp { file = name + ".meta", hash = FileHash(Folder + "/" + name + ".meta") });
            }
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(new Manifest { version = Version, assets = stamps.ToArray() }, true));
            AssetDatabase.ImportAsset(ManifestPath);
            return LoadPalette(plan);
        }

        private static void Lit(string name, Shader shader, Color color, Texture2D texture, Vector2 tiling, float metallic, float smoothness, Color emission)
        {
            color.a = 1f;
            var material = new Material(shader) { name = name, globalIlluminationFlags = MaterialGlobalIlluminationFlags.None };
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                if (texture != null)
                {
                    material.SetTexture("_EmissionMap", texture);
                    material.SetTextureScale("_EmissionMap", tiling);
                }
            }
            AssetDatabase.CreateAsset(material, Folder + "/" + name + ".mat");
        }

        private static Texture2D TextureAsset(string name, int pattern)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
                { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var pixels = new Color32[size * size];
            for (int vertical = 0; vertical < size; vertical++)
            {
                for (int horizontal = 0; horizontal < size; horizontal++)
                {
                    int edge = Math.Min(Math.Min(horizontal, size - 1 - horizontal), Math.Min(vertical, size - 1 - vertical));
                    byte shade = (byte)(158 + (horizontal * 13 + vertical * 7) % 9);
                    if (edge < 3 || horizontal == 63 || vertical == 63) shade = 42;
                    if (edge == 4) shade = 194;
                    if (pattern == 1) shade = (byte)(horizontal % 16 < 3 || vertical % 16 < 3 ? 174 : 48);
                    Color32 color = new Color32(shade, shade, shade, 255);
                    if (pattern == 2 && (vertical < 18 || vertical >= size - 18))
                        color = (horizontal + vertical) % 32 < 16 ? new Color32(242, 175, 35, 255) : new Color32(24, 27, 30, 255);
                    if (pattern != 1 && ((horizontal - 9) % 54 == 0) && ((vertical - 9) % 54 == 0)) color = new Color32(220, 220, 220, 255);
                    pixels[vertical * size + horizontal] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            AssetDatabase.CreateAsset(texture, Folder + "/" + name + ".asset");
            return texture;
        }

        private static List<string> AssetNames(Plan plan)
        {
            var names = new List<string> { "PanelGrid.asset", "Grating.asset", "Cargo.asset", "Core-Reactor.mat", "Night-Sky.mat" };
            foreach (Scope scope in plan.Scopes) names.Add(scope.Key + "-Floor.mat");
            foreach (Theme theme in Themes)
            {
                names.Add(theme.Key + "-Wall.mat");
                names.Add(theme.Key + "-Cover.mat");
                names.Add(theme.Key + "-Accent.mat");
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private static void ValidateAssets(Plan plan)
        {
            if (!File.Exists(ManifestPath)) throw new InvalidOperationException("The reserved visual asset folder exists without its ownership manifest. Partial or authored assets were preserved.");
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            var expected = new HashSet<string>(AssetNames(plan), StringComparer.Ordinal);
            foreach (string name in AssetNames(plan)) expected.Add(name + ".meta");
            if (manifest == null || manifest.version != Version || manifest.assets == null || manifest.assets.Length != expected.Count)
                throw new InvalidOperationException("Unexpected visual asset manifest. Nothing was overwritten.");
            foreach (Stamp stamp in manifest.assets)
            {
                if (stamp == null || stamp.file == null || !expected.Remove(stamp.file) || !MatchesAssetStamp(stamp))
                    throw new InvalidOperationException("Visual assets were edited, reimported or removed. Preserve those changes; no assets were overwritten.");
            }
            var allowed = new HashSet<string>(StringComparer.Ordinal) { "assets.json", "assets.json.meta" };
            foreach (Stamp stamp in manifest.assets) allowed.Add(stamp.file);
            foreach (string file in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
                if (!allowed.Contains(file.Substring(Folder.Length + 1).Replace('\\', '/')))
                    throw new InvalidOperationException("Unexpected authored file in the reserved visual asset folder: " + file);
        }

        /// <summary>Accepts original asset bytes or the exact reviewed saved sky variant, without rewriting its manifest or material.</summary>
        private static bool MatchesAssetStamp(Stamp stamp)
        {
            string actual = FileHash(Folder + "/" + stamp.file);
            return actual == stamp.hash || (stamp.file == "Night-Sky.mat" &&
                stamp.hash == "64acb21d6c2aa4b79bd6f4724ddabdb465164f4ce711935a8e73efcbbf68992f" &&
                actual == "98161302ff7226f8d28d5feb9d7e38a4f5855d702d0a88425529cdef6bfceb63");
        }

        private static Dictionary<string, Material> LoadPalette(Plan plan)
        {
            var palette = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (string name in AssetNames(plan))
            {
                if (!name.EndsWith(".mat", StringComparison.Ordinal)) continue;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name);
                if (material == null) throw new InvalidOperationException("Missing visual material: " + name);
                palette.Add(Path.GetFileNameWithoutExtension(name), material);
            }
            return palette;
        }

        private static bool MatchesLighting(Plan plan, Dictionary<string, Material> palette, bool legacy = false)
        {
            if (!Same(plan.Sun.color, legacy ? LegacySunColor : SunColor) || !Mathf.Approximately(plan.Sun.intensity, legacy ? 0.18f : SunIntensity) ||
                RenderSettings.skybox != palette["Night-Sky"] || RenderSettings.ambientMode != AmbientMode.Flat ||
                !Same(RenderSettings.ambientLight, legacy ? LegacyAmbient : Ambient) || !Mathf.Approximately(RenderSettings.reflectionIntensity, 0.35f)) return false;
            foreach (Scope scope in plan.Scopes)
            {
                if (scope.Light == null) continue;
                Theme theme = Themes[scope.Theme];
                if (!Same(scope.Light.color, theme.Accent) || !Mathf.Approximately(scope.Light.intensity, theme.Intensity) ||
                    !Mathf.Approximately(scope.Light.range, theme.Range)) return false;
            }
            return true;
        }

        private static void CheckOriginalLighting(Plan plan)
        {
            Material originalSky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (originalSky == null || RenderSettings.skybox != originalSky || RenderSettings.ambientMode != AmbientMode.Skybox ||
                !Same(RenderSettings.ambientSkyColor, new Color(0.212f, 0.227f, 0.259f)) ||
                !Mathf.Approximately(RenderSettings.reflectionIntensity, 1f))
                throw new InvalidOperationException("The original sky/ambient/reflection settings were manually edited. They were preserved.");
            if (!Mathf.Approximately(plan.Sun.intensity, 1f) || !Same(plan.Sun.color, new Color(1f, 0.95686275f, 0.8392157f)))
                throw new InvalidOperationException("The original directional lighting was manually edited. It was preserved.");
            foreach (Scope scope in plan.Scopes)
            {
                if (scope.Light == null) continue;
                if (!Mathf.Approximately(scope.Light.intensity, 3f) || !Mathf.Approximately(scope.Light.range, 24f) ||
                    !Same(scope.Light.color, Color.Lerp(Color.white, Themes[scope.Theme].OriginalAccent, 0.25f)))
                    throw new InvalidOperationException("Zone lighting was manually edited: " + scope.Path);
            }
        }

        private static string ProtectedState(Scene scene)
        {
            var rows = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    rows.Add(child.gameObject.GetInstanceID() + ":" + EditorJsonUtility.ToJson(child.gameObject));
                    foreach (Component component in child.GetComponents<Component>())
                    {
                        if (component == null) throw new InvalidOperationException("Missing script in Main: " + Hierarchy(child));
                        if (component is Renderer || component is Light) continue;
                        rows.Add(component.GetInstanceID() + ":" + EditorJsonUtility.ToJson(component));
                    }
                }
            }
            rows.Sort(StringComparer.Ordinal);
            return Hash(string.Join("\n", rows));
        }

        private static Dictionary<string, string> BakedAssetHashes(Scene scene)
        {
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (NavMeshSurface surface in root.GetComponentsInChildren<NavMeshSurface>(true))
                {
                    string path = AssetDatabase.GetAssetPath(surface.navMeshData);
                    if (string.IsNullOrEmpty(path)) continue;
                    hashes[path] = FileHash(path);
                    hashes[path + ".meta"] = FileHash(path + ".meta");
                }
            return hashes;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform found = null;
            foreach (Transform child in parent)
                if (child.name == name)
                {
                    if (found != null) throw new InvalidOperationException("Duplicate hierarchy name: " + name);
                    found = child;
                }
            if (found == null) throw new InvalidOperationException("Missing generated group: " + name);
            return found;
        }

        private static void CheckChildren(Transform parent, params string[] expected)
        {
            var remaining = new HashSet<string>(expected, StringComparer.Ordinal);
            foreach (Transform child in parent)
                if (!IsActor(child.gameObject) && !remaining.Remove(child.name))
                    throw new InvalidOperationException("Unexpected generated hierarchy content: " + Hierarchy(child));
            if (remaining.Count != 0) throw new InvalidOperationException("Generated hierarchy is incomplete: " + Hierarchy(parent));
        }

        private static bool IsActor(GameObject instance) => instance.GetComponent<IPlayerState>() != null || instance.GetComponent<IDamageable>() != null;
        private static void Count(Dictionary<string, int> counts, string key) { counts.TryGetValue(key, out int count); counts[key] = count + 1; }
        private static void RequireCount(Dictionary<string, int> counts, string key, int expected)
        {
            counts.TryGetValue(key, out int actual);
            if (actual != expected) throw new InvalidOperationException($"Unexpected {key} count: {actual}, expected {expected}.");
        }
        private static bool Same(Color first, Color second) => Mathf.Abs(first.r - second.r) < 0.0001f && Mathf.Abs(first.g - second.g) < 0.0001f && Mathf.Abs(first.b - second.b) < 0.0001f;
        private static string Number(float value) => (value == 0f ? 0f : value).ToString("F3", CultureInfo.InvariantCulture);
        private static string Format(Vector3 value) => Number(value.x) + "," + Number(value.y) + "," + Number(value.z);
        private static string Hierarchy(Transform value) => value.parent == null ? value.name : Hierarchy(value.parent) + "/" + value.name;
        private static string Hash(string value) { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
        private static string FileHash(string path) { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    }
}
