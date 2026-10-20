using System.Collections;
using System.IO;
using System.Linq;
using BlackoutProtocol.Body.Testing;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace BlackoutProtocol.Body.Tests
{
    /// <summary>Checks the actual supplied Titan assets, not a placeholder test controller.</summary>
    public sealed class TitanAnimationAssetTests
    {
        private const string Source = "Assets/_Project/ThirdParty/AgentBody/Titan/";
        private const string Owned = "Assets/_Project/Animations/AgentBody/Titan/";
        private const string Sandbox = "Assets/_Project/Scenes/Sandbox/Manula/AgentBodyAnimationSandbox.unity";

        [Test]
        public void RiggedModelHasValidHumanoidAvatarAndConsistentImportScale()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "Titan_Rigged.fbx");
            Assert.That(model, Is.Not.Null);
            var animator = model.GetComponent<Animator>();
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Source + "Titan_Rigged.fbx");
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.globalScale, Is.EqualTo(1));
            Assert.That(importer.useFileScale, Is.False);
            Assert.That(importer.humanDescription.skeleton.Length, Is.GreaterThan(0), "Keep the metre-scale Avatar T-pose configuration");
            Assert.That(model.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.GreaterThan(0));
        }

        [TestCase("Idle")]
        [TestCase("Walk")]
        [TestCase("Run")]
        public void ClipsShareTitanAvatarAndLoopWithBakedRootMotion(string name)
        {
            string path = Source + "Titan_" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Source + "Titan_Rigged.fbx").OfType<Avatar>().Single();
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CopyFromOther));
            Assert.That(importer.sourceAvatar, Is.EqualTo(avatar));
            Assert.That(importer.globalScale, Is.EqualTo(1));
            Assert.That(importer.useFileScale, Is.False);
            var settings = importer.clipAnimations.Single();
            Assert.That(settings.loopTime && settings.loopPose, Is.True);
            Assert.That(settings.lockRootRotation && settings.lockRootHeightY && settings.lockRootPositionXZ, Is.True);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => c.name == name);
            Assert.That(clip.humanMotion && clip.isLooping, Is.True);
            Assert.That(clip.length, Is.GreaterThan(0));
        }

        [Test]
        public void ControllerUsesOneSpeedBlendTreeWithIdleWalkRunThresholds()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Owned + "TitanLocomotion.controller");
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.parameters.Single().name, Is.EqualTo("Speed"));
            Assert.That(controller.parameters.Single().type, Is.EqualTo(AnimatorControllerParameterType.Float));
            var machine = controller.layers.Single().stateMachine;
            Assert.That(machine.states.Length, Is.EqualTo(1));
            var tree = (BlendTree)machine.defaultState.motion;
            Assert.That(tree.blendType, Is.EqualTo(BlendTreeType.Simple1D));
            Assert.That(tree.blendParameter, Is.EqualTo("Speed"));
            Assert.That(tree.children.Select(c => c.threshold), Is.EqualTo(new[] { 0f, 0.4f, 1f }));
            Assert.That(tree.children.Select(c => c.motion.name), Is.EqualTo(new[] { "Idle", "Walk", "Run" }));
            Assert.That(tree.children.Select(c => c.timeScale), Is.EqualTo(new[] { 1f, 1f, 0.65f }), "Only Run cadence is fitted to the unchanged 4 m/s body");
            Assert.That(machine.defaultState.transitions, Is.Empty);
        }

        [Test]
        public void SurfaceUsesOriginalMapsWithCorrectColorSpaceAndUrpKeywords()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Owned + "TitanSurface.mat");
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(Owned + "TitanVisual.prefab");
            Assert.That(visual.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial, Is.EqualTo(material));
            Assert.That(material.GetTexture("_BaseMap"), Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(Owned + "Textures/titan_basecolor.png")));
            Assert.That(material.GetTexture("_BumpMap"), Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(Owned + "Textures/titan_normal.png")));
            Assert.That(material.GetTexture("_MetallicGlossMap"), Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(Owned + "TitanMetallicSmoothness.png")));
            Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white));
            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
            Assert.That(material.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"), Is.False);
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1));
            Assert.That(material.GetFloat("_SmoothnessTextureChannel"), Is.EqualTo(0));
            foreach (string name in new[] { "basecolor", "normal", "metallic", "roughness" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Owned + "Textures/titan_" + name + ".png");
                Assert.That(importer.sRGBTexture, Is.EqualTo(name == "basecolor"), name);
                Assert.That(importer.textureType, Is.EqualTo(name == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default));
                Assert.That(importer.isReadable, Is.False, "Do not keep CPU copies in gameplay");
            }
            var packed = (TextureImporter)AssetImporter.GetAtPath(Owned + "TitanMetallicSmoothness.png");
            Assert.That(packed.sRGBTexture || packed.alphaIsTransparency, Is.False);
            Assert.That(packed.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
        }

        [Test]
        public void PackedSurfaceContainsMetallicRedAndInvertedRoughnessAlphaForEveryPixel()
        {
            var metallic = ReadPng(Owned + "Textures/titan_metallic.png");
            var roughness = ReadPng(Owned + "Textures/titan_roughness.png");
            var packed = ReadPng(Owned + "TitanMetallicSmoothness.png");
            try
            {
                Assert.That((packed.width, packed.height), Is.EqualTo((metallic.width, metallic.height)));
                Assert.That((packed.width, packed.height), Is.EqualTo((roughness.width, roughness.height)));
                Color32[] m = metallic.GetPixels32(), r = roughness.GetPixels32(), p = packed.GetPixels32();
                for (int i = 0; i < p.Length; i++)
                    if (p[i].r != m[i].r || p[i].a != 255 - r[i].r || p[i].g != 0 || p[i].b != 0)
                        Assert.Fail("Incorrect metallic/smoothness packing at pixel " + i);
            }
            finally
            {
                Object.DestroyImmediate(metallic); Object.DestroyImmediate(roughness); Object.DestroyImmediate(packed);
            }
        }

        [Test]
        public void RunPoseLoopsContinuouslyAtStableScaleWithoutVisualRootTravel()
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Owned + "TitanVisual.prefab"));
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            var clip = AssetDatabase.LoadAllAssetsAtPath(Source + "Titan_Run.fbx").OfType<AnimationClip>().Single(c => c.name == "Run");
            var graph = PlayableGraph.Create("Titan loop regression");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var motion = AnimationClipPlayable.Create(graph, clip);
                motion.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph, "pose", animator).SetSourcePlayable(motion);
                graph.Play(); motion.SetTime(0); graph.Evaluate(0);
                HumanBodyBones[] bones = { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                    HumanBodyBones.RightFoot, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };
                var positions = bones.Select(b => animator.GetBoneTransform(b).position).ToArray();
                var rotations = bones.Select(b => animator.GetBoneTransform(b).localRotation).ToArray();
                var thigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var knee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                float length = Vector3.Distance(thigh.position, knee.position);
                for (int sample = 1; sample <= 30; sample++)
                {
                    motion.SetTime((clip.length - 0.00001) * sample / 30); graph.Evaluate(0);
                    Assert.That(Vector3.Distance(thigh.position, knee.position), Is.EqualTo(length).Within(0.005f));
                    Assert.That(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.magnitude, Is.LessThan(2.5f));
                }
                for (int i = 0; i < bones.Length; i++)
                {
                    var bone = animator.GetBoneTransform(bones[i]);
                    Assert.That(Vector3.Distance(positions[i], bone.position), Is.LessThan(0.005f), bones[i].ToString());
                    Assert.That(Quaternion.Angle(rotations[i], bone.localRotation), Is.LessThan(1), bones[i].ToString());
                }
                Assert.That(model.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(animator.applyRootMotion, Is.False);
            }
            finally { graph.Destroy(); Object.DestroyImmediate(model); }
        }

        private static Texture2D ReadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
            return texture;
        }

        [Test]
        public void VisualPrefabIsReusableWithoutGroundMovementAndRootMotionIsOff()
        {
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(Owned + "TitanVisual.prefab");
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponentInChildren<AgentBody>(), Is.Null);
            Assert.That(visual.GetComponentInChildren<CharacterController>(), Is.Null);
            var animator = visual.GetComponent<Animator>();
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            var renderer = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
        }

        [UnityTest]
        public IEnumerator RealSandboxAnimatesWalkRunAndStopWithoutMovingTheVisualRoot()
        {
            EditorSceneManager.OpenScene(Sandbox);
            yield return new EnterPlayMode();
            // Construct the coroutine after domain reload: do not carry Unity objects across it.
            yield return VerifyPlayback();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyPlayback()
        {
            var demo = Object.FindFirstObjectByType<TitanAnimationSandbox>();
            Assert.That(demo, Is.Not.Null);
            var body = demo.GetComponent<AgentBody>();
            var driver = demo.GetComponent<AgentAnimatorDriver>();
            var animator = demo.GetComponentInChildren<Animator>();
            demo.StopDemo();
            yield return Elapse(0.5f);
            Vector3 visualLocal = animator.transform.localPosition;
            Assert.That(driver.IsAnimatorReady, Is.True);
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0).Within(0.001f));
            Assert.That(animator.isHuman, Is.True);
            Assert.That(animator.humanScale, Is.InRange(0.5f, 2f), "Valid alone is not enough: guard against centimetre-scale Avatar stretching");
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            demo.WalkRoute();
            yield return Elapse(1.5f);
            Assert.That(driver.ActualHorizontalSpeed, Is.EqualTo(1.6f).Within(0.12f));
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0.4f).Within(0.04f));
            Quaternion before = foot.localRotation;
            yield return Elapse(0.2f);
            Assert.That(Quaternion.Angle(before, foot.localRotation), Is.GreaterThan(0.1f), "Imported Humanoid pose must actually change");
            Assert.That(Vector3.Distance(foot.position, body.Position), Is.LessThan(2), "Foot must remain at plausible character scale during playback");
            demo.RunRoute();
            // Route replacement may initially turn toward the nearest route end.
            yield return Elapse(2);
            Assert.That(driver.ActualHorizontalSpeed, Is.EqualTo(4).Within(0.2f));
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(1).Within(0.08f));
            demo.StopDemo();
            Vector3 stopped = body.Position;
            yield return Elapse(1);
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0).Within(0.001f));
            Vector3 horizontalDelta = body.Position - stopped;
            horizontalDelta.y = 0;
            Assert.That(horizontalDelta.magnitude, Is.LessThan(0.001f));
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(Vector3.Distance(animator.transform.localPosition, visualLocal), Is.LessThan(0.001f));
            Assert.That(demo.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(demo.GetComponent<UnityEngine.AI.NavMeshAgent>(), Is.Null);
            demo.RestartAutomatic();
            Assert.That(demo.Automatic, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // These tests are hosted by the EditMode runner while explicitly entering
        // Play Mode. Yield frames against the game clock; WaitForSeconds is not an
        // EditMode test yield instruction and does not reliably wait here.
        private static IEnumerator Elapse(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }
    }
}
