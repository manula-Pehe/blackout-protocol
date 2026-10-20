using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BlackoutProtocol.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.Body.Tests
{
    public sealed class AgentAnimatorDriverTests
    {
        [Test]
        public void Mapping_UsesHorizontalActualSpeedAndConfigurableReference()
        {
            Assert.AreEqual(0, LocomotionAnimation.HorizontalSpeed(new Vector3(0, 12, 0)));
            Assert.AreEqual(0, LocomotionAnimation.TargetParameter(0, 4, 0.02f));
            Assert.AreEqual(0.4f, LocomotionAnimation.TargetParameter(1.6f, 4, 0.02f), 0.0001f);
            Assert.AreEqual(1, LocomotionAnimation.TargetParameter(4, 4, 0.02f));
            Assert.AreEqual(1.6f, LocomotionAnimation.TargetParameter(1.6f, 1, 0.02f));
            Assert.AreEqual(0, LocomotionAnimation.TargetParameter(0.01f, 4, 0.02f));
            Assert.AreEqual(5, LocomotionAnimation.HorizontalSpeed(new Vector3(3, 99, 4)));
        }

        [Test]
        public void Mapping_InvalidValuesAreSafeAndFinite()
        {
            Assert.AreEqual(0, LocomotionAnimation.HorizontalSpeed(new Vector3(float.NaN, 0, 0)));
            Assert.AreEqual(0, LocomotionAnimation.HorizontalSpeed(new Vector3(float.MaxValue, 0, float.MaxValue)));
            Assert.AreEqual(0, LocomotionAnimation.TargetParameter(float.PositiveInfinity, 4, 0.02f));
            Assert.AreEqual(1, LocomotionAnimation.TargetParameter(1, float.NaN, 0.02f));
            Assert.AreEqual(0, LocomotionAnimation.Smooth(float.NaN, float.PositiveInfinity, 0.1f, 0.02f));
            Assert.AreEqual(0.5f, LocomotionAnimation.Smooth(0.5f, 1, 0.1f, float.NaN));
        }

        [Test]
        public void Damping_AcceleratesBrakesAndReturnsSmoothlyToExactIdle()
        {
            float value = LocomotionAnimation.Smooth(0, 1, 0.1f, 0.02f);
            Assert.That(value, Is.InRange(0.01f, 0.99f));
            float slowed = LocomotionAnimation.Smooth(value, 0.1f, 0.1f, 0.02f);
            Assert.That(slowed, Is.InRange(0.1f, value));
            float stopped = LocomotionAnimation.Smooth(slowed, 0, 0.1f, 0.02f);
            Assert.That(stopped, Is.InRange(0.001f, slowed));
            for (int i = 0; i < 100; i++) stopped = LocomotionAnimation.Smooth(stopped, 0, 0.1f, 0.02f);
            Assert.AreEqual(0, stopped);
            Assert.AreEqual(1, LocomotionAnimation.Smooth(0, 1, 0, 0.02f));
        }

        [Test]
        public void Damping_FramePartitionAndZeroDeltaPreserveHistory()
        {
            float once = LocomotionAnimation.Smooth(0.2f, 1, 0.1f, 0.1f);
            float split = 0.2f;
            for (int i = 0; i < 5; i++) split = LocomotionAnimation.Smooth(split, 1, 0.1f, 0.02f);
            Assert.AreEqual(once, split, 0.00001f);
            Assert.AreEqual(split, LocomotionAnimation.Smooth(split, 0.4f, 0.1f, 0));
        }

        [UnityTest]
        public IEnumerator RealAnimator_ActualWalkRunBrakingStopAndReplacementDriveParameter()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyMovementParameter();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyMovementParameter()
        {
            AgentBody body = MakeBody();
            using (var rig = new ParameterRig(body, "MotionSpeed"))
            {
                Set(rig.Driver, "referenceSpeed", 4f);
                Set(rig.Driver, "speedDampTime", 0.15f);
                yield return Until(() => rig.Driver.IsAnimatorReady, 3);
                Assert.AreEqual(0, rig.Driver.ActualHorizontalSpeed);
                Assert.AreEqual(0, rig.Animator.GetFloat("MotionSpeed"));
                Assert.IsFalse(rig.Animator.applyRootMotion);
                Vector3 goal = Vector3.forward * 12;
                body.Execute(new MoveCommand(new[] { goal }, MoveStyle.Walk));
                yield return Until(() => body.Velocity.magnitude > 1.55f && rig.Driver.SmoothedSpeedParameter > 0.38f, 4);
                Assert.AreEqual(0.4f, rig.Driver.TargetSpeedParameter, 0.025f);
                Assert.AreEqual(rig.Driver.SmoothedSpeedParameter, rig.Animator.GetFloat("MotionSpeed"), 0.0001f);
                float beforeReplacement = rig.Driver.SmoothedSpeedParameter;
                body.Execute(new MoveCommand(new[] { body.Position, goal }, MoveStyle.Run));
                Assert.AreEqual(beforeReplacement, rig.Driver.SmoothedSpeedParameter, "Execute must not reset damping history.");
                yield return Until(() => body.Velocity.magnitude > 3.9f && rig.Driver.SmoothedSpeedParameter > 0.97f, 4);
                Assert.AreEqual(1, rig.Driver.TargetSpeedParameter, 0.03f);
                body.Execute(new MoveCommand(new[] { goal }, MoveStyle.Walk));
                yield return Until(() => body.Velocity.magnitude < 3 && body.Velocity.magnitude > 1.7f, 3);
                Assert.That(rig.Driver.SmoothedSpeedParameter, Is.InRange(0.4f, 1.03f));
                body.Stop();
                Vector3 stoppedAt = body.Position;
                yield return Until(() => rig.Driver.TargetSpeedParameter == 0, 2);
                Assert.Greater(rig.Driver.SmoothedSpeedParameter, 0, "Animation fades after the immediate physical Stop.");
                yield return Until(() => rig.Driver.SmoothedSpeedParameter == 0, 3);
                Assert.AreEqual(0, rig.Animator.GetFloat("MotionSpeed"));
                Assert.Less(PathFollower.HorizontalDistance(body.Position, stoppedAt), 0.001f);
                Assert.IsFalse(rig.Animator.applyRootMotion);
            }
        }

        [UnityTest]
        public IEnumerator MissingWrongDisabledAndSwappedAnimatorBindingsAreSafe()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyBindings();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyBindings()
        {
            AgentBody body = MakeBody();
            var driver = body.gameObject.AddComponent<AgentAnimatorDriver>();
            yield return null;
            Assert.AreEqual(AgentAnimatorDriver.BindingStatus.MissingAnimator, driver.Status);
            using (var rig = new ParameterRig(body, "Speed", driver))
            {
                yield return Until(() => driver.IsAnimatorReady, 3);
                RuntimeAnimatorController valid = rig.Animator.runtimeAnimatorController;
                var wrong = rig.CreateController("Speed", AnimatorControllerParameterType.Bool);
                rig.Animator.runtimeAnimatorController = wrong;
                yield return Until(() => driver.Status == AgentAnimatorDriver.BindingStatus.MissingFloatParameter, 3);
                int validations = driver.BindingValidationCount;
                for (int i = 0; i < 5; i++) yield return null;
                Assert.AreEqual(validations, driver.BindingValidationCount, "Do not allocate a parameter list every frame.");
                rig.Animator.runtimeAnimatorController = null;
                yield return Until(() => driver.Status == AgentAnimatorDriver.BindingStatus.MissingController, 3);
                rig.Animator.runtimeAnimatorController = valid;
                yield return Until(() => driver.IsAnimatorReady, 3);
                rig.Animator.enabled = false;
                yield return Until(() => driver.Status == AgentAnimatorDriver.BindingStatus.InactiveAnimator, 3);
                rig.Animator.enabled = true;
                yield return Until(() => driver.IsAnimatorReady, 3);
                driver.enabled = false;
                Assert.AreEqual(0, driver.SmoothedSpeedParameter);
                Assert.IsFalse(driver.IsAnimatorReady);
                driver.enabled = true;
                yield return Until(() => driver.IsAnimatorReady, 3);
                var replacement = new GameObject("GV transient replacement Animator");
                replacement.transform.SetParent(body.transform, false);
                var replacementAnimator = replacement.AddComponent<Animator>();
                replacementAnimator.runtimeAnimatorController = valid;
                replacementAnimator.applyRootMotion = true;
                var binding = new SerializedObject(driver);
                binding.FindProperty("animator").objectReferenceValue = replacementAnimator;
                binding.ApplyModifiedPropertiesWithoutUndo();
                yield return Until(() => driver.IsAnimatorReady && !replacementAnimator.applyRootMotion, 3);
                binding.Update();
                binding.FindProperty("animator").objectReferenceValue = rig.Animator;
                binding.ApplyModifiedPropertiesWithoutUndo();
                yield return Until(() => driver.IsAnimatorReady, 3);
                Object.Destroy(replacement);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator HunterActualSoundResponse_UsesSameDriverWithoutBrainChanges()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity");
            yield return new EnterPlayMode();
            yield return VerifyHunter();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyHunter()
        {
            yield return null;
            AgentBody body = Object.FindFirstObjectByType<AgentBody>();
            Assert.IsNotNull(body);
            Type brainType = Type.GetType("BlackoutProtocol.AI.Hunter.HunterBrain, BlackoutProtocol.Hunter.Runtime", true);
            var brain = (Behaviour)body.GetComponent(brainType);
            Assert.IsNotNull(brain);
            object ReadBrain(string property) => brainType.GetProperty(property).GetValue(brain);

            // Attach while the real Hunter is already moving. This deliberately covers
            // an initialized Animator becoming Ready before the driver's first LateUpdate.
            yield return Until(() => LocomotionAnimation.HorizontalSpeed(body.Velocity) > 0.1f, 4,
                () => $"Hunter={ReadBrain("CurrentAction")}, status={ReadBrain("Status")}, " +
                    $"pathFailures={ReadBrain("PathFailureCount")}, velocity={body.Velocity}, waypoint={body.CurrentWaypointIndex}/{body.Waypoints.Count}");
            using (var rig = new ParameterRig(body, "Speed"))
            {
                string Snapshot() => $"frame={Time.frameCount}, dt={Time.deltaTime:F6}, Hunter={ReadBrain("CurrentAction")}, " +
                    $"status={ReadBrain("Status")}, accepted={ReadBrain("AcceptedNoiseCount")}, " +
                    $"pathRequests={ReadBrain("PathRequestCount")}, pathFailures={ReadBrain("PathFailureCount")}, " +
                    $"style={body.Style}, moving={body.IsMoving}, grounded={body.GetComponent<AgentMotor>().IsGrounded}, " +
                    $"waypoint={body.CurrentWaypointIndex}/{body.Waypoints.Count}, velocity={body.Velocity}, " +
                    $"binding={rig.Driver.Status}, initialized={rig.Animator.isInitialized}, " +
                    $"sampled={rig.Driver.ActualHorizontalSpeed:F6}, target={rig.Driver.TargetSpeedParameter:F6}, " +
                    $"smoothed={rig.Driver.SmoothedSpeedParameter:F6}, stored={rig.Animator.GetFloat("Speed"):F6}";
                bool MovingThroughAnimator(float minimumSpeed) => rig.Driver.IsAnimatorReady &&
                    LocomotionAnimation.HorizontalSpeed(body.Velocity) > minimumSpeed &&
                    rig.Driver.ActualHorizontalSpeed > minimumSpeed && rig.Driver.TargetSpeedParameter > 0 &&
                    rig.Animator.GetFloat("Speed") > 0;

                Vector3 initialPosition = body.Position;
                // Ready only describes parameter binding, not a sampled movement frame.
                // Wait for actual displacement AND LateUpdate's output to reach Animator.
                yield return Until(() => MovingThroughAnimator(0.1f) &&
                    PathFollower.HorizontalDistance(body.Position, initialPosition) > 0.02f, 4, Snapshot);
                Assert.Greater(rig.Driver.TargetSpeedParameter, 0);
                Assert.Greater(rig.Animator.GetFloat("Speed"), 0);
                Assert.AreEqual("Wander", ReadBrain("CurrentAction").ToString());
                Debug.Log("Hunter animator Wander pipeline: " + Snapshot());

                int accepted = (int)ReadBrain("AcceptedNoiseCount"), requests = (int)ReadBrain("PathRequestCount");
                Vector3 soundStart = body.Position;
                GameEvents.RaiseNoise(new NoiseEvent(body.Position + Vector3.forward * 3, NoiseLevels.Gunshot, NoiseType.Gunshot, Time.time));
                Assert.AreEqual(accepted + 1, (int)ReadBrain("AcceptedNoiseCount"), "Shared sound event must reach the Hunter.");
                yield return Until(() => ReadBrain("CurrentAction").ToString() == "Chase" &&
                    (int)ReadBrain("PathRequestCount") > requests && body.Style == MoveStyle.Run &&
                    MovingThroughAnimator(0.2f) && PathFollower.HorizontalDistance(body.Position, soundStart) > 0.02f, 4, Snapshot);
                Assert.Greater(rig.Driver.TargetSpeedParameter, 0);
                Assert.Greater(rig.Animator.GetFloat("Speed"), 0);
                Assert.Greater(body.Waypoints.Count, 0);
                Assert.AreEqual(rig.Driver.ActualHorizontalSpeed, rig.Driver.TargetSpeedParameter, 0.0001f);
                Assert.AreEqual(rig.Driver.SmoothedSpeedParameter, rig.Animator.GetFloat("Speed"), 0.0001f);
                Assert.IsFalse(rig.Animator.applyRootMotion);
                Debug.Log("Hunter animator sound/Chase pipeline: " + Snapshot());

                // OnDisable is the real Hunter lifecycle cancellation path: it stops
                // its body. No synthetic MoveCommand or Animator value is injected.
                brain.enabled = false;
                Vector3 stoppedAt = body.Position;
                yield return Until(() => !body.IsMoving && LocomotionAnimation.HorizontalSpeed(body.Velocity) == 0 &&
                    rig.Driver.ActualHorizontalSpeed == 0 && rig.Driver.TargetSpeedParameter == 0 &&
                    rig.Driver.SmoothedSpeedParameter == 0 && rig.Animator.GetFloat("Speed") == 0, 3, Snapshot);
                Assert.AreEqual(0, rig.Animator.GetFloat("Speed"));
                Assert.Less(PathFollower.HorizontalDistance(body.Position, stoppedAt), 0.001f);
                Debug.Log("Hunter animator cancelled/Idle pipeline: " + Snapshot());
            }
        }

        [UnityTest]
        public IEnumerator BreacherActualBeginAndCancel_DriveMovementThenIdleParameter()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/Manula/HunterSandbox.unity");
            foreach (MonoBehaviour component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (component.GetType().Name == "HunterSandboxRouteDriver") component.enabled = false;
            yield return new EnterPlayMode();
            yield return VerifyBreacher();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyBreacher()
        {
            AgentBody body = Object.FindFirstObjectByType<AgentBody>();
            Type agentType = Type.GetType("BlackoutProtocol.AI.Breacher.BreacherAgent, Assembly-CSharp", true);
            var agent = (Behaviour)body.gameObject.AddComponent(agentType);
            agent.enabled = false;
            var approach = new GameObject("GV transient animation test approach");
            approach.transform.position = new Vector3(6, 0, -3);
            agentType.GetField("doorApproach", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(agent, approach.transform);
            Type actionType = Type.GetType("BlackoutProtocol.AI.Breacher.Logic.MoveToAction, BlackoutProtocol.Breacher.Logic", true);
            object empty = Activator.CreateInstance(Type.GetType("BlackoutProtocol.AI.Breacher.Logic.WorldState, BlackoutProtocol.Breacher.Logic", true));
            object action = Activator.CreateInstance(actionType, "Door", empty, empty, empty, 1f);
            using (var rig = new ParameterRig(body, "Speed"))
            {
                yield return Until(() => rig.Driver.IsAnimatorReady, 3);
                agentType.GetMethod("Begin").Invoke(agent, new[] { action });
                yield return Until(() => body.Velocity.magnitude > 0.3f && rig.Driver.TargetSpeedParameter > 0.1f, 4);
                Assert.AreEqual(MoveStyle.Run, body.Style);
                Assert.AreEqual("Running", agentType.GetMethod("Tick").Invoke(agent, new[] { action }).ToString());
                agentType.GetMethod("Cancel").Invoke(agent, new[] { action });
                yield return Until(() => rig.Driver.TargetSpeedParameter == 0 && rig.Driver.SmoothedSpeedParameter == 0, 3);
                Assert.IsFalse(body.HasArrived);
                Assert.AreEqual(0, rig.Animator.GetFloat("Speed"));
                Assert.AreEqual("Failed", agentType.GetMethod("Tick").Invoke(agent, new[] { action }).ToString());
            }
        }

        // Only an in-memory float controller and an empty motion: no character, Avatar,
        // real Idle/Walk/Run clip, saved test asset or visual-animation claim is fabricated.
        private sealed class ParameterRig : IDisposable
        {
            private readonly List<Object> owned = new List<Object>();
            public Animator Animator { get; }
            public AgentAnimatorDriver Driver { get; }

            public ParameterRig(AgentBody body, string parameter, AgentAnimatorDriver existing = null)
            {
                var visual = new GameObject("GV parameter-only test Animator (no rig)");
                visual.transform.SetParent(body.transform, false);
                owned.Add(visual);
                Animator = visual.AddComponent<Animator>();
                Animator.runtimeAnimatorController = CreateController(parameter, AnimatorControllerParameterType.Float);
                Animator.applyRootMotion = true; // Driver must override this before playback.
                Driver = existing != null ? existing : body.gameObject.AddComponent<AgentAnimatorDriver>();
                var settings = new SerializedObject(Driver);
                settings.FindProperty("speedParameter").stringValue = parameter;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Driver.RefreshAnimatorBinding();
            }

            public AnimatorController CreateController(string parameter, AnimatorControllerParameterType type)
            {
                var controller = new AnimatorController { name = "GV transient parameter controller", hideFlags = HideFlags.DontSave };
                owned.Add(controller);
                controller.AddParameter(parameter, type);
                controller.AddLayer("Parameter test only");
                var machine = controller.layers[0].stateMachine;
                owned.Add(machine);
                var state = machine.AddState("No pose animation");
                owned.Add(state);
                var clip = new AnimationClip { name = "GV empty parameter-test motion", hideFlags = HideFlags.DontSave };
                owned.Add(clip);
                state.motion = clip;
                machine.defaultState = state;
                return controller;
            }

            public void Dispose()
            {
                Driver.enabled = false;
                Animator.runtimeAnimatorController = null;
                for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            }
        }

        private static AgentBody MakeBody()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -0.25f, 0);
            floor.transform.localScale = new Vector3(30, 0.5f, 30);
            var body = new GameObject("GV animation-parameter test body").AddComponent<AgentBody>();
            var controller = body.GetComponent<CharacterController>();
            controller.center = Vector3.up;
            controller.height = 2;
            controller.radius = 0.25f;
            Physics.SyncTransforms();
            return body;
        }

        private static void Set(Object target, string name, float value)
        {
            var settings = new SerializedObject(target);
            settings.FindProperty(name).floatValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerator Until(Func<bool> condition, float timeout, Func<string> diagnostics = null)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            if (!condition()) Assert.Fail("Timed out waiting for the animation parameter pipeline. " + diagnostics?.Invoke());
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
