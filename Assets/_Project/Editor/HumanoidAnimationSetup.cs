using System;
using System.Linq;
using BlackoutProtocol.Body;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackoutProtocol.EditorTools
{
    public sealed class HumanoidAnimationSetup : EditorWindow
    {
        private const string Folder = "Assets/_Project/Animations/AgentBody/Titan/Completed";
        private static readonly string[] Labels = { "Idle", "Walk Forward", "Run Forward", "Walk Backward", "Run Backward", "Walk Left", "Run Left", "Walk Right", "Run Right", "Sneak", "Attack", "Hit", "Death", "Alert" };
        private AnimationClip[] clips = new AnimationClip[14];
        private float hitFrame = 0.45f;
        private Vector2 scroll;
        [MenuItem("Blackout Protocol/Animation/Build Humanoid Controller")]
        private static void Open() => GetWindow<HumanoidAnimationSetup>("Humanoid Animation");
        private void OnEnable()
        {
            string[] guids = { "817e54c2cd411f4d79584ff086ee019c", "7936ed7af5cf5e6f1968de6e9ca32436", "5c2278cc2d174e27eac0b78cf02fab25" };
            for (int i = 0; i < 3; i++) if (clips[i] == null)
                clips[i] = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guids[i])).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            TitanClipImportSetup.FillMissingSlots(clips);
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Assign compatible Humanoid clips. Existing idle/walk/run are preselected. Build creates separate assets and leaves the original Titan controller intact. Open Main before applying.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < clips.Length; i++) clips[i] = (AnimationClip)EditorGUILayout.ObjectField(Labels[i], clips[i], typeof(AnimationClip), false);
            hitFrame = EditorGUILayout.Slider("Attack impact (normalized)", hitFrame, 0.05f, 0.95f);
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || clips.Any(c => c == null || !c.humanMotion)))
                if (GUILayout.Button("Build new controller")) Build();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/TitanCompleted.controller") == null))
                if (GUILayout.Button("Apply to humanoids in open scene")) Apply();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); string parent = path.Substring(0, slash);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
        private void Build()
        {
            EnsureFolder(Folder);
            string controllerPath = Folder + "/TitanCompleted.controller";
            if (AssetDatabase.LoadMainAssetAtPath(controllerPath) != null) { EditorUtility.DisplayDialog("Already built", "Controller already exists. Move the Completed folder to a backup location in the Project window before rebuilding.", "OK"); return; }
            var motions = new AnimationClip[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                var copy = Instantiate(clips[i]); copy.name = Labels[i];
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = i < 10; AnimationUtility.SetAnimationClipSettings(copy, settings);
                if (i > 0 && i < 10) AnimationUtility.SetAnimationEvents(copy, new[] { Event("AnimationFootstep", copy.length * 0.18f), Event("AnimationFootstep", copy.length * 0.68f) });
                else if (i == 10) AnimationUtility.SetAnimationEvents(copy, new[] { Event("AnimationAttackImpact", copy.length * hitFrame) });
                else AnimationUtility.SetAnimationEvents(copy, Array.Empty<AnimationEvent>());
                AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + Labels[i] + ".anim")); motions[i] = copy;
            }
            var c = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (string name in new[] { "Speed", "LocalX", "LocalZ", "StrideRate", "AttackRate" }) c.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = name == "StrideRate" || name == "AttackRate" ? 1 : 0 });
            c.AddParameter("Sneak", AnimatorControllerParameterType.Bool);
            foreach (string name in new[] { "Attack", "Hit", "Die", "Alert" }) c.AddParameter(name, AnimatorControllerParameterType.Trigger);
            var machine = c.layers[0].stateMachine;
            var locomotion = machine.AddState("Directional Locomotion"); machine.defaultState = locomotion;
            var tree = new BlendTree { name = "Directional Walk Run", blendType = BlendTreeType.FreeformDirectional2D, blendParameter = "LocalX", blendParameterY = "LocalZ", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, c);
            Vector2[] points = { Vector2.zero, new Vector2(0,.4f), new Vector2(0,1), new Vector2(0,-.4f), new Vector2(0,-1), new Vector2(-.4f,0), new Vector2(-1,0), new Vector2(.4f,0), new Vector2(1,0) };
            for (int i = 0; i < points.Length; i++) tree.AddChild(motions[i], points[i]);
            locomotion.motion = tree; locomotion.speedParameter = "StrideRate"; locomotion.speedParameterActive = true;
            var sneak = machine.AddState("Sneak"); sneak.motion = motions[9]; sneak.speedParameter = "StrideRate"; sneak.speedParameterActive = true;
            Condition(locomotion.AddTransition(sneak), AnimatorConditionMode.If, "Sneak"); Condition(sneak.AddTransition(locomotion), AnimatorConditionMode.IfNot, "Sneak");
            var hit = machine.AddState("Hit"); hit.motion = motions[11]; Any(machine, hit, "Hit"); Exit(hit, locomotion);
            var die = machine.AddState("Death"); die.motion = motions[12]; Any(machine, die, "Die");
            var mask = new AvatarMask(); mask.name = "Upper Body";
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers }) mask.SetHumanoidBodyPartActive(part, true);
            AssetDatabase.CreateAsset(mask, AssetDatabase.GenerateUniqueAssetPath(Folder + "/UpperBody.mask"));
            c.AddLayer("Upper Body"); var layers = c.layers; layers[1].avatarMask = mask; layers[1].defaultWeight = 0; layers[1].blendingMode = AnimatorLayerBlendingMode.Override; c.layers = layers;
            var upper = layers[1].stateMachine; var empty = upper.AddState("No Action"); upper.defaultState = empty; empty.writeDefaultValues = false;
            var attack = upper.AddState("Attack"); attack.motion = motions[10]; attack.speedParameter = "AttackRate"; attack.speedParameterActive = true; Any(upper, attack, "Attack"); Exit(attack, empty);
            var alert = upper.AddState("Alert"); alert.motion = motions[13]; Any(upper, alert, "Alert"); Exit(alert, empty);
            EditorUtility.SetDirty(c); AssetDatabase.SaveAssets(); Selection.activeObject = c;
            Debug.Log("Created " + controllerPath + ". Apply to Main and verify clip timing before committing.");
        }
        private static AnimationEvent Event(string function, float time) => new AnimationEvent { functionName = function, time = time };
        private static void Condition(AnimatorStateTransition transition, AnimatorConditionMode mode, string parameter) { transition.hasExitTime = false; transition.duration = 0.1f; transition.AddCondition(mode, 0, parameter); }
        private static void Any(AnimatorStateMachine machine, AnimatorState state, string parameter) { var t = machine.AddAnyStateTransition(state); t.canTransitionToSelf = false; Condition(t, AnimatorConditionMode.If, parameter); }
        private static void Exit(AnimatorState from, AnimatorState to) { var t = from.AddTransition(to); t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.08f; }
        private static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/TitanCompleted.controller");
            int count = 0; Scene scene = SceneManager.GetActiveScene();
            if (!EditorUtility.DisplayDialog("Apply animations", "Modify humanoid AgentBody visuals in the active scene? Save the scene after reviewing.", "Apply", "Cancel")) return;
            foreach (var root in scene.GetRootGameObjects()) foreach (var body in root.GetComponentsInChildren<AgentBody>(true))
            {
                var animator = body.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman) continue;
                Undo.RecordObject(animator, "Assign completed humanoid controller"); animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                if (!animator.TryGetComponent<AgentActionAnimation>(out _)) Undo.AddComponent<AgentActionAnimation>(animator.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator); count++;
            }
            EditorSceneManager.MarkSceneDirty(scene); Debug.Log($"Updated {count} humanoid visuals in {scene.name}. Save the scene after reviewing.");
        }
    }
}
