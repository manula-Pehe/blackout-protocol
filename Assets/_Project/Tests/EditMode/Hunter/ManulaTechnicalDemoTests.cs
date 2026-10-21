using System;
using System.Collections;
using BlackoutProtocol.AI.Hunter.Logic;
using BlackoutProtocol.AI.Hunter.Testing;
using BlackoutProtocol.Body;
using BlackoutProtocol.Core;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.AI.Hunter.Tests
{
    public sealed class ManulaTechnicalDemoTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/Manula/ManulaTechnicalDemo.unity";
        private static ManulaTechnicalDemo Demo => Object.FindFirstObjectByType<ManulaTechnicalDemo>();

        [Test]
        public void SavedComposition_ReopensWithOwnedVisualAndBakeWithoutTeamSystems()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(ScenePath);
            Assert.AreEqual(1, Object.FindObjectsByType<HunterBrain>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<AgentBody>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.NotNull(Demo.Signals.TestTarget);
            Assert.IsFalse(Demo.Signals.TestTarget.gameObject.activeSelf);
            Assert.IsNull(Demo.Hunter.GetComponent<HunterSandboxRouteDriver>());
            Assert.AreEqual("Assets/_Project/Scripts/AgentBody/AgentAnimatorDriver.cs", AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(Demo.AnimationDriver)));
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            Assert.NotNull(surface.navMeshData);
            Assert.AreEqual("Assets/_Project/Scenes/Sandbox/Manula/HunterSandboxAssets/HunterSandboxNavMesh.asset", AssetDatabase.GetAssetPath(surface.navMeshData));
            var mesh = Demo.Hunter.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.NotNull(mesh);
            Assert.AreEqual("Assets/_Project/Animations/AgentBody/Titan/TitanSurface.mat", AssetDatabase.GetAssetPath(mesh.sharedMaterial));
            foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string type = component.GetType().FullName;
                Assert.IsFalse(type.StartsWith("BlackoutProtocol.Player.") || type.Contains(".Breacher.") ||
                    type.Contains(".Flanker.") || type.Contains(".Sentinel."), "Unexpected team system: " + type);
            }
            Assert.IsEmpty(Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None));
        }

        [TestCase(1280, 800)]
        [TestCase(1920, 1080)]
        [TestCase(800, 600)]
        public void ControlLayout_StaysVisibleAndNonOverlapping(float width, float height)
        {
            Rect previous = default;
            for (var control = ManulaTechnicalDemo.Control.Alarm; control <= ManulaTechnicalDemo.Control.Overview; control++)
            {
                Rect rect = ManulaTechnicalDemo.GetControlRect(control, width, height);
                Assert.Greater(rect.width, 0); Assert.Greater(rect.height, 0);
                Assert.GreaterOrEqual(rect.xMin, 0); Assert.LessOrEqual(rect.xMax, width);
                Assert.GreaterOrEqual(rect.yMin, 0); Assert.LessOrEqual(rect.yMax, height);
                if (control != ManulaTechnicalDemo.Control.Alarm) Assert.IsFalse(rect.Overlaps(previous));
                previous = rect;
            }
        }

        [UnityTest]
        public IEnumerator Wander_UsesRealTexturedTitanAnimationAndCachedSearchComparison()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyWander();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyWander()
        {
            yield return Until(() => Moving(Demo, 0.2f), 8);
            var demo = Demo;
            Assert.AreEqual(HunterAction.Wander, demo.Hunter.CurrentAction);
            var renderer = demo.Hunter.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = demo.Hunter.GetComponentInChildren<Animator>();
            Assert.IsTrue(animator.isHuman); Assert.IsFalse(animator.applyRootMotion);
            Assert.NotNull(renderer.sharedMaterial.GetTexture("_BaseMap"));
            Assert.NotNull(renderer.sharedMaterial.GetTexture("_BumpMap"));
            Assert.NotNull(renderer.sharedMaterial.GetTexture("_MetallicGlossMap"));
            Assert.That(renderer.bounds.size.y, Is.InRange(1f, 3f));
            Assert.Less(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.Head).position,
                animator.GetBoneTransform(HumanBodyBones.LeftFoot).position), 3f);
            Assert.Greater(animator.GetFloat("Speed"), 0);
            var nav = demo.Hunter.GetComponent<HunterNavMeshPathfinder>();
            Assert.Greater(NavMesh.CalculateTriangulation().indices.Length, 0);
            Assert.IsTrue(nav.LastSearch.Succeeded);
            int builds = nav.GraphBuildCount, searches = nav.AStarSearchCount;
            var panel = demo.Hunter.GetComponent<HunterDebugPanel>();
            Assert.IsTrue(panel.CompareLatestRoute());
            Assert.AreEqual(nav.LastSearch.TotalCost, nav.LastUniformCostSearch.TotalCost, 1e-6);
            Assert.AreEqual(builds, nav.GraphBuildCount); Assert.AreEqual(searches, nav.AStarSearchCount);
            panel.RefreshDisplay();
            foreach (HunterAction action in Enum.GetValues(typeof(HunterAction)))
                Assert.AreEqual(demo.Hunter.LastSelection.BaseScores.Get(action), panel.GetDisplayedScoreRow(action).BaseScore);
        }

        [UnityTest]
        public IEnumerator ImpactsHabituate_WhileGunshotSelectsChaseAndRunAnimation()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifySignals();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifySignals()
        {
            yield return Until(() => Moving(Demo, 0.1f), 8);
            var demo = Demo;
            int heard = demo.Hunter.AcceptedNoiseCount;
            demo.ExecuteControl(ManulaTechnicalDemo.Control.Impact);
            demo.ExecuteControl(ManulaTechnicalDemo.Control.RepeatedDecoy);
            Assert.AreEqual(heard + 6, demo.Hunter.AcceptedNoiseCount);
            yield return Until(() => demo.Hunter.HasSelectedNoise && demo.Hunter.SelectedNoise.Observation.Category == NoiseType.Impact, 3);
            Assert.LessOrEqual(demo.Hunter.SelectedNoise.EffectiveTrust, 0.4f);
            int requests = demo.Hunter.PathRequestCount;
            demo.ExecuteControl(ManulaTechnicalDemo.Control.Gunshot);
            Assert.AreEqual(heard + 7, demo.Hunter.AcceptedNoiseCount);
            yield return Until(() => demo.Hunter.CurrentAction == HunterAction.Chase && demo.Hunter.PathRequestCount > requests &&
                demo.Hunter.GetComponent<AgentBody>().Style == MoveStyle.Run && Moving(demo, 1.7f) &&
                demo.AnimationDriver.SmoothedSpeedParameter > 0.4f, 8);
            Assert.AreEqual(NoiseType.Gunshot, demo.Hunter.SelectedNoise.Observation.Category);
            Assert.Greater(demo.Hunter.SelectedNoise.EffectiveTrust, 0.9f);
            Assert.Greater(demo.Hunter.GetComponentInChildren<Animator>().GetFloat("Speed"), 0.4f);
        }

        [UnityTest]
        public IEnumerator Alarm_FollowsCornerDetourArrivesSearchesAndReturnsToWander()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyDetour();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyDetour()
        {
            yield return Until(() => Moving(Demo, 0.1f), 8);
            var demo = Demo; var body = demo.Hunter.GetComponent<AgentBody>();
            int heard = demo.Hunter.AcceptedNoiseCount, arrivals = demo.Hunter.ArrivalCount;
            demo.ExecuteControl(ManulaTechnicalDemo.Control.Alarm);
            Assert.AreEqual(heard + 1, demo.Hunter.AcceptedNoiseCount);
            yield return Until(() => demo.Hunter.CurrentAction == HunterAction.Investigate && demo.Hunter.HasActiveRoute, 4);
            Assert.IsTrue(NavMesh.Raycast(body.Position, demo.Hunter.Destination, out _, NavMesh.AllAreas));
            float end = Time.realtimeSinceStartup + 55, highest = body.Position.z;
            while (demo.Hunter.ArrivalCount == arrivals && Time.realtimeSinceStartup < end)
            {
                highest = Mathf.Max(highest, body.Position.z);
                if (Mathf.Abs(body.Position.x) < 0.85f) Assert.Greater(body.Position.z, 2.25f, "Hunter crossed the wall instead of the corner.");
                yield return null;
            }
            Assert.Greater(demo.Hunter.ArrivalCount, arrivals);
            Assert.Greater(highest, 2.25f);
            Assert.Greater(body.Position.x, 1);
            yield return Until(() => demo.Hunter.CurrentAction == HunterAction.Search, 4);
            yield return Until(() => demo.Hunter.CurrentAction == HunterAction.Wander, 15);
            Assert.AreEqual(0, demo.Hunter.PathFailureCount);
        }

        [UnityTest]
        public IEnumerator PauseStopsToIdle_ResumeRecovers_WithoutObsoleteArrivalOrCameraCommandsMovingBody()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyPause();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyPause()
        {
            yield return Until(() => Moving(Demo, 0.2f), 8);
            var demo = Demo; var body = demo.Hunter.GetComponent<AgentBody>();
            demo.ExecuteControl(ManulaTechnicalDemo.Control.Pause);
            Vector3 stopped = body.Position;
            int arrivals = demo.Hunter.ArrivalCount, heard = demo.Hunter.AcceptedNoiseCount, controls = demo.ControlsExecuted;
            demo.ExecuteControl(ManulaTechnicalDemo.Control.Gunshot);
            Assert.AreEqual(controls, demo.ControlsExecuted); Assert.AreEqual(heard, demo.Hunter.AcceptedNoiseCount);
            Assert.IsTrue(demo.Paused); Assert.IsFalse(body.IsMoving); Assert.AreEqual(Vector3.zero, body.Velocity);
            yield return Until(() => demo.AnimationDriver.SmoothedSpeedParameter == 0 &&
                demo.Hunter.GetComponentInChildren<Animator>().GetFloat("Speed") == 0, 3);
            demo.ExecuteControl(ManulaTechnicalDemo.Control.Overview); Assert.IsFalse(demo.Following);
            yield return null;
            demo.ExecuteControl(ManulaTechnicalDemo.Control.Follow); Assert.IsTrue(demo.Following);
            yield return new WaitForSeconds(0.4f);
            Assert.Less(PathFollower.HorizontalDistance(stopped, body.Position), 0.001f);
            Assert.AreEqual(arrivals, demo.Hunter.ArrivalCount);
            demo.ExecuteControl(ManulaTechnicalDemo.Control.Resume);
            yield return Until(() => !demo.Paused && Moving(demo, 0.1f), 8);
        }

        [UnityTest]
        public IEnumerator SimulatedTarget_SelectsAttackWithoutRealPlayer_AndHideRecovers()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyTarget();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyTarget()
        {
            yield return Until(() => Moving(Demo, 0.1f), 8);
            var demo = Demo;
            demo.ExecuteControl(ManulaTechnicalDemo.Control.ShowTarget);
            Assert.AreSame(demo.Signals.TestTarget, PlayerRegistry.Current);
            yield return Until(() => demo.Hunter.CurrentAction == HunterAction.Attack && demo.Hunter.AttackCount > 0, 12);
            Assert.IsFalse(demo.Hunter.GetComponent<AgentBody>().IsMoving);
            Assert.IsTrue(demo.Signals.TestTarget.IsAlive);
            demo.ExecuteControl(ManulaTechnicalDemo.Control.HideTarget);
            Assert.IsNull(PlayerRegistry.Current);
            yield return Until(() => demo.Hunter.CurrentAction != HunterAction.Attack && Moving(demo, 0.05f), 8);
        }

        private static bool Moving(ManulaTechnicalDemo demo, float minimum) => demo != null && demo.AnimationDriver.IsAnimatorReady &&
            demo.AnimationDriver.ActualHorizontalSpeed > minimum && demo.AnimationDriver.TargetSpeedParameter > 0;

        private static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(condition(), Demo == null ? "Missing demo harness" :
                $"Timeout: action={Demo.Hunter.CurrentAction}, {Demo.Hunter.Status}, {Demo.AnimationDriver.Status}, speed={Demo.AnimationDriver.ActualHorizontalSpeed}");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
