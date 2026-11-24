using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BlackoutProtocol.EditorTools
{
    public static class TitanClipImportSetup
    {
        private const string Folder = "Assets/_Project/Animations/AgentBody/Titan";
        // Order matches HumanoidAnimationSetup slots 3 through 13.
        private static readonly string[] Names = { "Walking Backwards", "Running Backward", "Left Strafe Walking", "Left Strafe", "Right Strafe Walking", "Right Strafe", "Sneak Walk", "Mutant Punch", "Hit Reaction", "Death", "Zombie Transition" };

        private static string Find(string name)
        {
            var matches = AssetDatabase.FindAssets("t:Model", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(System.IO.Path.GetFileName(p), name + ".fbx", StringComparison.OrdinalIgnoreCase)).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        [MenuItem("Blackout Protocol/Animation/Configure downloaded Titan clips")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string[] paths = Names.Select(Find).ToArray();
            if (paths.Any(p => p == null))
            {
                Debug.LogError("Missing or duplicate FBX: " + string.Join(", ", Names.Where((n,i) => paths[i] == null)));
                return;
            }
            int good = 0;
            for (int i = 0; i < paths.Length; i++)
            {
                var importer = AssetImporter.GetAtPath(paths[i]) as ModelImporter;
                if (importer == null) { Debug.LogError("Not a model importer: " + paths[i]); continue; }
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.SaveAndReimport();
                importer = (ModelImporter)AssetImporter.GetAtPath(paths[i]);
                var clips = importer.clipAnimations;
                if (clips.Length == 0) clips = importer.defaultClipAnimations;
                if (clips.Length == 0) { Debug.LogError("No clips in " + paths[i]); continue; }
                bool loop = i < 7;
                foreach (var clip in clips)
                {
                    clip.loopTime = loop;
                    clip.loopPose = loop;
                    if (loop)
                    {
                        clip.lockRootRotation = true;
                        clip.lockRootHeightY = true;
                        clip.lockRootPositionXZ = true;
                    }
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                var assets = AssetDatabase.LoadAllAssetsAtPath(paths[i]);
                bool valid = assets.OfType<Avatar>().Any(a => a.isValid && a.isHuman) && assets.OfType<AnimationClip>().Any(c => c.humanMotion && !c.name.StartsWith("__preview__"));
                if (valid) good++;
                else Debug.LogError("Humanoid import needs inspection: " + paths[i]);
            }
            Debug.Log($"Titan clip configuration complete: {good}/{Names.Length} validated Humanoid imports. Preview gait, death and attack before building.");
            EditorUtility.DisplayDialog("Titan clips", $"Configured and validated {good}/{Names.Length} Humanoid FBXs. See Console for any failures. Reopen Build Humanoid Controller to auto-fill slots.", "OK");
        }

        public static void FillMissingSlots(AnimationClip[] slots)
        {
            for (int i = 0; i < Names.Length; i++)
            {
                if (slots[i + 3] != null) continue;
                string path = Find(Names[i]);
                if (path == null) continue;
                slots[i + 3] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => c.humanMotion && !c.name.StartsWith("__preview__"));
            }
        }
    }
}
