using System;
using System.Collections;
using System.Reflection;
using BlackoutProtocol.AI.Hunter.Logic;
using BlackoutProtocol.AI.Hunter.Testing;
using BlackoutProtocol.Body;
using BlackoutProtocol.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.AI.Hunter.Tests
{
    public sealed class HunterDebugTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity";

        [Test]
        public void TransitionHistory_IsBoundedNewestFirstAndStoresActualSelections()
        {
            var history = new HunterTransitionHistory(3);
            var selector = new HunterUtilitySelector();
            var chase = selector.Select(new HunterUtilityContext(1, 1, 1, 0.8f, 0, 0));
            for (int i = 0; i < 5; i++)
                history.Record(new HunterActionTransition(i, HunterAction.Wander, chase, HunterDecisionReason.TrustedSound));
            history.Record(new HunterActionTransition(6, chase.Action, chase, HunterDecisionReason.UtilityScores));
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.GetRecent(0).Time, Is.EqualTo(4));
            Assert.That(history.GetRecent(2).Time, Is.EqualTo(2));
            Assert.That(history.GetRecent(0).Reason, Is.EqualTo(HunterDecisionReason.TrustedSound));
            Assert.That(history.GetRecent(0).Selection.Scores.Chase, Is.EqualTo(chase.Scores.Chase));
            Assert.Throws<ArgumentOutOfRangeException>(() => history.GetRecent(3));
        }

        [UnityTest]
        public IEnumerator Panel_CopiesActualScoresComparesCachedRouteAndHandlesF1()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyPanel();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyPanel()
        {
            yield return new WaitForSeconds(0.3f);
            var brain = Object.FindFirstObjectByType<HunterBrain>();
            var panel = brain.GetComponent<HunterDebugPanel>();
            var pathfinder = brain.GetComponent<HunterNavMeshPathfinder>();
            Assert.That(panel, Is.Not.Null);
            panel.RefreshDisplay();
            for (HunterAction action = HunterAction.Wander; action <= HunterAction.Attack; action++)
            {
                HunterDebugScoreRow row = panel.GetDisplayedScoreRow(action);
                Assert.That(row.BaseScore, Is.EqualTo(brain.LastSelection.BaseScores.Get(action)));
                Assert.That(row.AdjustedScore, Is.EqualTo(brain.LastSelection.Scores.Get(action)));
                Assert.That(row.Selected, Is.EqualTo(brain.LastSelection.Action == action));
            }
            Assert.That(panel.DisplayedDecisionTime, Is.EqualTo(brain.LastDecisionTime));
            int decisions = brain.DecisionCount, aStars = pathfinder.AStarSearchCount;
            int ucs = pathfinder.UniformCostSearchCount, builds = pathfinder.GraphBuildCount;
            HunterPathResult actualAStar = pathfinder.LastSearch;
            for (int i = 0; i < 5; i++) panel.RefreshDisplay();
            Assert.That(brain.DecisionCount, Is.EqualTo(decisions));
            Assert.That(pathfinder.AStarSearchCount, Is.EqualTo(aStars));
            Assert.That(pathfinder.UniformCostSearchCount, Is.EqualTo(ucs));
            Assert.That(panel.CompareLatestRoute(), Is.True);
            Assert.That(pathfinder.LastSearch, Is.SameAs(actualAStar));
            Assert.That(pathfinder.LastUniformCostSearch.TotalCost, Is.EqualTo(actualAStar.TotalCost).Within(1e-6));
            Assert.That(pathfinder.GraphBuildCount, Is.EqualTo(builds));
            panel.CompareLatestRoute();
            Assert.That(pathfinder.UniformCostSearchCount, Is.EqualTo(ucs)); // already compared on request

            panel.SetCompareOnRouteRequests(false);
            GameEvents.RaiseNoise(new NoiseEvent(new Vector3(6, 0, -3), NoiseLevels.Alarm, NoiseType.Alarm, Time.time));
            yield return WaitFor(() => pathfinder.AStarSearchCount > aStars, 3);
            Assert.That(pathfinder.LastUniformCostSearch, Is.Null);
            actualAStar = pathfinder.LastSearch;
            Assert.That(panel.CompareLatestRoute(), Is.True);
            Assert.That(pathfinder.LastSearch, Is.SameAs(actualAStar));
            Assert.That(pathfinder.UniformCostSearchCount, Is.EqualTo(ucs + 1));
            Assert.That(pathfinder.LastUniformCostSearch.TotalCost, Is.EqualTo(actualAStar.TotalCost).Within(1e-6));
            panel.CompareLatestRoute();
            Assert.That(pathfinder.UniformCostSearchCount, Is.EqualTo(ucs + 1));

            panel.SetCompareOnRouteRequests(true);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputSettings.UpdateMode previousUpdateMode = InputSystem.settings.updateMode;
            InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSettings.BackgroundBehavior previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            try
            {
                // These are EditMode tests entering Play Mode: explicitly process dynamic
                // input instead of depending on Game view focus in the headless runner.
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.Update();
                keyboard.MakeCurrent();
                Assert.That(keyboard.f1Key.wasPressedThisFrame, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                InputSystem.Update();
                Assert.That(keyboard.f1Key.wasPressedThisFrame, Is.True, "Synthetic F1 state must reach the current input update buffer.");
                panel.SendMessage("Update");
                Assert.That(panel.OverlayVisible, Is.False);
                Assert.That(pathfinder.CompareOnRouteRequest, Is.False);
                Assert.That(Object.FindFirstObjectByType<HunterSandboxNoiseTrigger>().isActiveAndEnabled, Is.True);
                Assert.That(brain.isActiveAndEnabled, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                panel.SendMessage("Update");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                InputSystem.Update();
                panel.SendMessage("Update");
                Assert.That(panel.OverlayVisible, Is.True);
                Assert.That(pathfinder.CompareOnRouteRequest, Is.True);
            }
            finally
            {
                InputSystem.settings.updateMode = previousUpdateMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
                InputSystem.RemoveDevice(keyboard);
            }
            Assert.That(brain.Transitions.Count, Is.GreaterThan(0));
            Assert.That(brain.Transitions.GetRecent(0).Selection.Action, Is.EqualTo(brain.CurrentAction));
        }

        [UnityTest]
        public IEnumerator ExpiredSound_LeavesSafeEmptyDebugSnapshotAndFiniteScores()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var settings = new SerializedObject(Object.FindFirstObjectByType<HunterBrain>());
            settings.FindProperty("memoryHalfLife").floatValue = 0.3f;
            settings.FindProperty("memoryMaximumAge").floatValue = 0.5f;
            settings.FindProperty("searchDuration").floatValue = 0.5f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return VerifyExpiry();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyExpiry()
        {
            yield return null;
            var brain = Object.FindFirstObjectByType<HunterBrain>();
            var panel = brain.GetComponent<HunterDebugPanel>();
            GameEvents.RaiseNoise(new NoiseEvent(brain.transform.position + Vector3.forward * 5, 50, NoiseType.Alarm, Time.time));
            yield return WaitFor(() => brain.HasSelectedNoise, 2);
            panel.RefreshDisplay();
            Assert.That(panel.HasDisplayedNoise, Is.True);
            Assert.That(panel.DisplayedNoise.Confidence, Is.EqualTo(brain.SelectedNoise.Confidence));
            Assert.That(panel.DisplayedNoiseAge, Is.GreaterThanOrEqualTo(0));
            yield return WaitFor(() => !brain.HasSelectedNoise && brain.CurrentAction == HunterAction.Wander, 5);
            panel.RefreshDisplay();
            Assert.That(panel.HasDisplayedNoise, Is.False);
            Assert.That(panel.DisplayedNoise.Confidence, Is.Zero);
            Assert.That(panel.DisplayedNoiseAge, Is.Zero);
            Assert.That(panel.MemoryText, Does.Contain("none"));
            for (HunterAction action = HunterAction.Wander; action <= HunterAction.Attack; action++)
            {
                float score = panel.GetDisplayedScoreRow(action).AdjustedScore;
                Assert.That(float.IsNaN(score) || float.IsInfinity(score), Is.False);
            }
            Assert.That(brain.AttackCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RepeatedStuckAndUnreachableSound_CoolDownAndSearchLocallyWithoutRebuildSpam()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyRecovery();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyRecovery()
        {
            yield return null;
            var brain = Object.FindFirstObjectByType<HunterBrain>();
            var body = brain.GetComponent<AgentBody>();
            var pathfinder = brain.GetComponent<HunterNavMeshPathfinder>();
            GameEvents.RaiseNoise(new NoiseEvent(new Vector3(6, 0, -3), 50, NoiseType.Alarm, Time.time));
            yield return WaitFor(() => brain.CurrentAction == HunterAction.Investigate && body.IsMoving, 3);
            int requests = brain.PathRequestCount, builds = pathfinder.GraphBuildCount;
            RaiseStuck(body);
            for (int i = 0; i < 10; i++) RaiseStuck(body);
            Assert.That(brain.StuckRecoveryCount, Is.EqualTo(1));
            Assert.That(pathfinder.GraphBuildCount, Is.EqualTo(builds));
            yield return new WaitForSeconds(0.25f);
            Assert.That(brain.PathRequestCount, Is.EqualTo(requests));
            yield return WaitFor(() => brain.PathRequestCount > requests && body.IsMoving, 3);
            RaiseStuck(body);
            yield return WaitFor(() => brain.CurrentAction == HunterAction.Search, 2);
            Assert.That(brain.StuckRecoveryCount, Is.EqualTo(2));
            Assert.That(brain.Transitions.GetRecent(0).Reason, Is.EqualTo(HunterDecisionReason.RepeatedStuck));
            Assert.That(pathfinder.GraphBuildCount, Is.EqualTo(builds + 1));

            GameEvents.RaiseNoise(new NoiseEvent(new Vector3(1000, 0, 1000), 2000, NoiseType.Alarm, Time.time));
            yield return WaitFor(() => brain.PathFailureCount > 0, 3);
            Assert.That(pathfinder.LastSearch, Is.Null, "An unsampled endpoint must clear previous successful metrics.");
            Assert.That(pathfinder.LastUniformCostSearch, Is.Null);
            Assert.That(pathfinder.CanCompareLastSearch, Is.False);
            var panel = brain.GetComponent<HunterDebugPanel>();
            Assert.That(panel.CompareLatestRoute(), Is.False);
            Assert.That(panel.ComparisonText, Does.Contain("not run"));
            Assert.That(pathfinder.NavigationStatus, Does.Contain("Goal"));
            int failures = brain.PathFailureCount;
            yield return WaitFor(() => brain.CurrentAction == HunterAction.Search, 2);
            yield return new WaitForSeconds(1);
            Assert.That(brain.PathFailureCount, Is.EqualTo(failures));
            Assert.That(pathfinder.GraphBuildCount, Is.EqualTo(builds + 1));
        }

        [UnityTest]
        public IEnumerator DestroyedRegisteredTarget_CancelsAttackWithoutDereferencingDestroyedTransform()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyDestroyedTarget();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyDestroyedTarget()
        {
            yield return null;
            var brain = Object.FindFirstObjectByType<HunterBrain>();
            var target = Object.FindFirstObjectByType<HunterSandboxNoiseTrigger>().TestTarget;
            target.transform.position = brain.transform.position + Vector3.forward;
            target.gameObject.SetActive(true);
            Physics.SyncTransforms();
            yield return WaitFor(() => brain.AttackCount > 0, 3);
            int attacks = brain.AttackCount;
            Object.Destroy(target.gameObject);
            yield return null;
            PlayerRegistry.Register(target); // Deliberately stale Unity-object wrapper behind an interface.
            yield return WaitFor(() => brain.LastContext.AttackEligibility == 0 && brain.CurrentAction != HunterAction.Attack, 3);
            Assert.That(brain.AttackCount, Is.EqualTo(attacks));
            Assert.That(brain.HasVisibleTarget, Is.False);
            Assert.That(brain.LastContext.AttackEligibility, Is.Zero);
            Assert.That(brain.CurrentAction, Is.Not.EqualTo(HunterAction.Attack));
        }

        private static void RaiseStuck(AgentBody body) => typeof(AgentBody)
            .GetMethod("RaiseStuck", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(body, null);

        private static IEnumerator WaitFor(Func<bool> predicate, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Timed out waiting for Hunter debug/robustness behavior.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (PlayerRegistry.Current != null) PlayerRegistry.Unregister(PlayerRegistry.Current);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
