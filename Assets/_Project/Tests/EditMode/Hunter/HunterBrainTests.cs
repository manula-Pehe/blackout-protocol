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
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.AI.Hunter.Tests
{
    public sealed class HunterBrainTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity";

        [Test]
        public void ResolvedLoudObservation_DoesNotHideANewerQuieterObservation()
        {
            var memory = new HunterSoundMemory();
            memory.Record(new HunterNoiseObservation(0, 0, 0, NoiseType.Alarm, 1, 50, 1), 1);
            memory.Record(new HunterNoiseObservation(1, 0, 0, NoiseType.Impact, 2, 12, 0.6f), 2);
            Assert.That(memory.TryGetMostRelevant(2, out HunterRememberedNoise strongest), Is.True);
            Assert.That(strongest.Observation.Category, Is.EqualTo(NoiseType.Alarm));
            Assert.That(memory.TryGetMostRelevant(2, 1, out HunterRememberedNoise unresolved), Is.True);
            Assert.That(unresolved.Observation.Category, Is.EqualTo(NoiseType.Impact));
            Assert.That(memory.TryGetMostRelevant(2, double.NaN, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator Autonomy_WandersInvestigatesAroundWallAndRecoversFromStuck()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyAutonomy();
            yield return new ExitPlayMode();
        }

        // Captured runtime variables must be created AFTER EnterPlayMode's domain reload.
        private static IEnumerator VerifyAutonomy()
        {
            yield return new WaitForSeconds(0.3f);
            var brain = FindBrain();
            var body = brain.GetComponent<AgentBody>();
            Assert.That(brain.CurrentAction, Is.EqualTo(HunterAction.Wander));
            Assert.That(body.IsMoving, Is.True);
            var driver = brain.GetComponent<HunterSandboxRouteDriver>();
            driver.enabled = true;
            Assert.That(driver.enabled, Is.False, "Route driver must refuse ownership while the brain is active.");
            Assert.That(driver.BeginRoute(), Is.False);

            Vector3 goal = new Vector3(6, 0, -3);
            int arrivals = brain.ArrivalCount;
            GameEvents.RaiseNoise(new NoiseEvent(goal, NoiseLevels.Alarm, NoiseType.Alarm, Time.time));
            yield return WaitUntil(() => (brain.CurrentAction == HunterAction.Investigate ||
                brain.CurrentAction == HunterAction.Chase) && body.IsMoving, 3);
            Assert.That(brain.GetComponent<HunterNavMeshPathfinder>().LastSearch.Succeeded, Is.True);
            int requests = brain.PathRequestCount;
            yield return new WaitForSeconds(0.25f);
            Assert.That(brain.PathRequestCount, Is.EqualTo(requests), "Identical destination should reuse its route.");
            RaiseBodyStuck(body);
            Assert.That(brain.StuckRecoveryCount, Is.EqualTo(1));
            Assert.That(body.IsMoving, Is.False);
            yield return WaitUntil(() => brain.PathRequestCount > requests && body.IsMoving, 3);

            float maximumZ = body.Position.z;
            float deadline = Time.realtimeSinceStartup + 40;
            while (brain.ArrivalCount == arrivals && Time.realtimeSinceStartup < deadline)
            {
                maximumZ = Mathf.Max(maximumZ, body.Position.z);
                if (Mathf.Abs(body.Position.x) < 0.85f)
                    Assert.That(body.Position.z, Is.GreaterThan(2.25f), "Hunter crossed the obstacle footprint.");
                yield return null;
            }
            Assert.That(brain.ArrivalCount, Is.GreaterThan(arrivals), brain.Status);
            Assert.That(maximumZ, Is.GreaterThan(2.25f));
            Vector3 difference = body.Position - brain.Destination;
            difference.y = 0;
            Assert.That(difference.magnitude, Is.LessThan(0.6f), "Arrival must be at the perceived acoustic estimate.");
            Assert.That(brain.SelectedNoise.Observation.UncertaintyRadius, Is.GreaterThan(0));
            yield return WaitUntil(() => brain.CurrentAction == HunterAction.Search, 2);
            Assert.That(brain.AttackCount, Is.Zero, "A remembered noise is not an attack target.");
            Debug.Log($"Hunter Stage B verification: investigated around wall, arrived at {body.Position}, maximum Z={maximumZ:F2}, stuck recoveries={brain.StuckRecoveryCount}.");
        }

        [UnityTest]
        public IEnumerator SoundDecay_SelectsChaseThenSearchThenWanderWithoutRediscoveringOldSound()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var settings = new SerializedObject(Object.FindFirstObjectByType<HunterBrain>());
            settings.FindProperty("memoryHalfLife").floatValue = 1.2f;
            settings.FindProperty("memoryMaximumAge").floatValue = 4f;
            settings.FindProperty("searchDuration").floatValue = 1.2f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return VerifyDecay();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyDecay()
        {
            yield return null;
            var brain = FindBrain();
            Vector3 noisePoint = brain.transform.position + Vector3.forward * 3;
            GameEvents.RaiseNoise(new NoiseEvent(noisePoint, NoiseLevels.Gunshot, NoiseType.Gunshot, Time.time));
            yield return WaitUntil(() => brain.CurrentAction == HunterAction.Chase &&
                brain.GetComponent<AgentBody>().Style == MoveStyle.Run, 2);
            Assert.That(brain.GetComponent<AgentBody>().Style, Is.EqualTo(MoveStyle.Run));
            yield return WaitUntil(() => brain.CurrentAction == HunterAction.Search, 4);
            yield return WaitUntil(() => brain.CurrentAction == HunterAction.Wander, 4);
            yield return new WaitForSeconds(2);
            Assert.That(brain.CurrentAction, Is.EqualTo(HunterAction.Wander));
            Assert.That(brain.AcceptedNoiseCount, Is.EqualTo(1));
            Assert.That(brain.AttackCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Lifecycle_RejectsInvalidAndUnreachableNoiseAndUnsubscribes()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyLifecycle();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LevelChangesAreDebounced_AndSentinelAlarmBecomesOneAcousticObservation()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyLevelChangeAndAlarm();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyLevelChangeAndAlarm()
        {
            yield return new WaitForSeconds(0.3f);
            HunterBrain brain = FindBrain();
            HunterNavMeshPathfinder pathfinder = brain.GetComponent<HunterNavMeshPathfinder>();
            int builds = pathfinder.GraphBuildCount;
            GameEvents.RaiseLevelChanged(null);
            GameEvents.RaiseLevelChanged(null);
            yield return new WaitForSeconds(0.2f);
            Assert.That(brain.NavigationChangeCount, Is.Zero, "Graph rebuild must wait for NavMesh carving.");
            Assert.That(pathfinder.GraphBuildCount, Is.EqualTo(builds));
            yield return WaitUntil(() => brain.NavigationChangeCount == 1 && pathfinder.GraphBuildCount == builds + 1, 3);

            int accepted = brain.AcceptedNoiseCount;
            Vector3 alarm = new Vector3(6, 0, -3);
            GameEvents.RaiseAlarm(alarm);
            Assert.That(brain.AcceptedNoiseCount, Is.EqualTo(accepted + 1));
            Assert.That(brain.SelectedNoise.Observation.Category == NoiseType.Alarm ||
                brain.LastRuntimeEvent.Contains("Acoustic evidence"), Is.True);
            GameEvents.RaiseNoise(new NoiseEvent(alarm, NoiseLevels.Alarm, NoiseType.Alarm, Time.time));
            Assert.That(brain.AcceptedNoiseCount, Is.EqualTo(accepted + 1), "A mirrored alarm noise must not be double-counted.");
        }

        private static IEnumerator VerifyLifecycle()
        {
            yield return null;
            var brain = FindBrain();
            var body = brain.GetComponent<AgentBody>();
            int accepted = brain.AcceptedNoiseCount;
            GameEvents.RaiseNoise(new NoiseEvent(new Vector3(float.NaN, 0, 0), 50, NoiseType.Alarm, Time.time));
            GameEvents.RaiseNoise(new NoiseEvent(body.Position, float.PositiveInfinity, NoiseType.Alarm, Time.time));
            GameEvents.RaiseNoise(new NoiseEvent(body.Position, 50, NoiseType.Alarm, Time.time + 100));
            GameEvents.RaiseNoise(new NoiseEvent(body.Position + Vector3.right * 100, 1, NoiseType.Impact, Time.time));
            GameEvents.RaiseNoise(new NoiseEvent(body.Position, 50, NoiseType.Alarm, Time.time, brain.gameObject));
            Assert.That(brain.AcceptedNoiseCount, Is.EqualTo(accepted));

            brain.enabled = false;
            int stuck = brain.StuckRecoveryCount;
            GameEvents.RaiseNoise(new NoiseEvent(body.Position, 50, NoiseType.Alarm, Time.time));
            RaiseBodyStuck(body);
            Assert.That(brain.AcceptedNoiseCount, Is.EqualTo(accepted));
            Assert.That(brain.StuckRecoveryCount, Is.EqualTo(stuck));
            Assert.That(body.IsMoving, Is.False);
            brain.enabled = true;
            GameEvents.RaiseNoise(new NoiseEvent(new Vector3(1000, 0, 1000), 2000, NoiseType.Alarm, Time.time));
            Assert.That(brain.AcceptedNoiseCount, Is.EqualTo(accepted), "Off-NavMesh sound cannot invent a reachable source anchor.");
            Assert.That(brain.GetComponent<HunterNavMeshPathfinder>().AcousticStatus, Does.Contain("outside"));
            brain.enabled = false;
            brain.enabled = true;
            yield return null;
            int before = brain.AcceptedNoiseCount;
            GameEvents.RaiseNoise(new NoiseEvent(body.Position, NoiseLevels.Gunshot, NoiseType.Gunshot, Time.time));
            Assert.That(brain.AcceptedNoiseCount, Is.EqualTo(before + 1), "Re-enabling must install exactly one noise listener.");
            RaiseBodyStuck(body);
            Assert.That(brain.StuckRecoveryCount, Is.EqualTo(stuck + 1), "Body event listener must also be installed exactly once.");
        }

        [UnityTest]
        public IEnumerator Attack_RequiresRecentlyHeardLiveRegisteredTargetAndActualRange()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return VerifyAttack();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyAttack()
        {
            yield return null;
            var brain = FindBrain();
            var trigger = Object.FindFirstObjectByType<HunterSandboxNoiseTrigger>();
            var target = trigger.TestTarget;
            target.transform.position = new Vector3(1.5f, 0, -3);
            target.gameObject.SetActive(true);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.3f);
            Assert.That(brain.HasVisibleTarget, Is.False, "The wall must block vision.");
            Assert.That(brain.AttackCount, Is.Zero);

            target.transform.position = brain.transform.position + Vector3.forward * 3;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.5f);
            Assert.That(brain.CurrentAction, Is.EqualTo(HunterAction.Wander),
                "A silent target outside melee reach must not create visual pursuit.");
            GameEvents.RaiseNoise(new NoiseEvent(target.Position, NoiseLevels.Gunshot, NoiseType.Gunshot, Time.time, target.gameObject));
            yield return WaitUntil(() => brain.CurrentAction == HunterAction.Chase, 2);
            Assert.That(brain.AttackCount, Is.Zero, "A heard target outside the attack radius is not eligible.");
            float health = target.HealthNormalized;
            yield return WaitUntil(() => brain.AttackCount > 0, 6);
            Assert.That(target.HealthNormalized, Is.LessThan(health));
            Assert.That(Vector3.Distance(brain.transform.position, target.Position), Is.LessThanOrEqualTo(1.4f));
            Assert.That(brain.GetComponent<AgentBody>().IsMoving, Is.False);
            int attacks = brain.AttackCount;
            target.TakeDamage(1000, target.Position, brain.gameObject);
            PlayerRegistry.Register(target); // Deliberately stale dead-player registry entry.
            yield return new WaitForSeconds(1.5f);
            Assert.That(brain.AttackCount, Is.EqualTo(attacks));
            target.gameObject.SetActive(false);
            PlayerRegistry.Register(target); // Deliberately stale inactive-player registry entry.
            yield return new WaitForSeconds(1.5f);
            Assert.That(brain.AttackCount, Is.EqualTo(attacks));
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            HunterBrain brain = Object.FindFirstObjectByType<HunterBrain>();
            Assert.That(condition(), Is.True, brain == null ? "Missing HunterBrain." :
                $"Timed out: action={brain.CurrentAction}, status={brain.Status}, event={brain.LastRuntimeEvent}, accepted={brain.AcceptedNoiseCount}, requests={brain.PathRequestCount}, failures={brain.PathFailureCount}.");
        }

        private static HunterBrain FindBrain()
        {
            var brain = Object.FindFirstObjectByType<HunterBrain>();
            Assert.That(brain, Is.Not.Null, "HunterNoiseSandbox must contain an active HunterBrain.");
            return brain;
        }

        private static void RaiseBodyStuck(AgentBody body)
        {
            // Trigger the real existing AgentBody event without modifying shared movement code.
            MethodInfo method = typeof(AgentBody).GetMethod("RaiseStuck", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(body, null);
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
