using System;
using System.Collections;
using System.Reflection;
using BlackoutProtocol.Body.Testing;
using BlackoutProtocol.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.Body.Tests
{
    public sealed class AgentBodySteeringTests
    {
        [UnityTest]
        public IEnumerator OriginalSandbox_CompletesBothDirectionsWithoutStuck()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/Manula/AgentBodySandbox.unity");
            yield return new EnterPlayMode();
            yield return VerifySandbox();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifySandbox()
        {
            var body = Object.FindFirstObjectByType<AgentBody>();
            int arrivals = 0, stuck = 0;
            string stuckDetails = "";
            body.Arrived += () => arrivals++;
            body.Stuck += () =>
            {
                stuck++;
                stuckDetails = $"position={body.Position}, waypoint={body.CurrentWaypointIndex}, speed={body.CurrentSpeed}, dt={Time.deltaTime}, minMove={body.GetComponent<CharacterController>().minMoveDistance}";
            };
            Assert.IsTrue(body.GetComponent<WaypointRouteDriver>().isActiveAndEnabled);
            yield return Until(() => arrivals >= 2 || stuck > 0, 65);
            Assert.AreEqual(0, stuck, stuckDetails);
            Assert.AreEqual(2, arrivals);
        }

        [UnityTest]
        public IEnumerator Runtime_CornersAndFinalApproachAreConservative()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyCorners();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyCorners()
        {
            AgentBody body = CreateBody();
            int arrived = 0, stuck = 0;
            body.Arrived += () => arrived++;
            body.Stuck += () => stuck++;
            body.Execute(new MoveCommand(new[] { Vector3.zero, new Vector3(0, 0, 2),
                new Vector3(2, 0, 2), new Vector3(2, 0, 4) }, MoveStyle.Run));
            float deadline = Time.realtimeSinceStartup + 20;
            float approachSpeed = 0;
            while (body.IsMoving && Time.realtimeSinceStartup < deadline)
            {
                if (body.Position.x > 0.1f) Assert.GreaterOrEqual(body.Position.z, 1.91f);
                if (body.CurrentWaypointIndex == 1 && body.Position.z > 1.9f)
                    Assert.Less(body.CurrentSpeed, 0.8f);
                if (body.CurrentWaypointIndex == 3 && PathFollower.HorizontalDistance(body.Position, new Vector3(2, 0, 4)) < 0.3f)
                    approachSpeed = body.CurrentSpeed;
                yield return null;
            }
            Assert.AreEqual(1, arrived);
            Assert.AreEqual(0, stuck);
            Assert.Less(PathFollower.HorizontalDistance(body.Position, new Vector3(2, 0, 4)), 0.16f);
            Assert.Greater(approachSpeed, 0);
            Assert.Less(approachSpeed, 0.9f);
            Assert.AreEqual(0, body.CurrentSpeed);
        }

        [UnityTest]
        public IEnumerator Runtime_ReplacementStopInvalidAndReentrantArrivalRespectCommandLifetime()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyCommands();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyCommands()
        {
            AgentBody body = CreateBody();
            int arrivals = 0;
            body.Arrived += () => arrivals++;
            body.Execute(new MoveCommand(new[] { Vector3.forward * 12 }, MoveStyle.Run));
            yield return Until(() => body.CurrentSpeed > 2, 3);
            Vector3 replacement = body.Position + new Vector3(-2, 0, 1);
            body.Execute(new MoveCommand(new[] { body.Position, replacement }, MoveStyle.Walk));
            Assert.IsFalse(body.HasArrived);
            Assert.AreEqual(0, arrivals);
            yield return Until(() => body.HasArrived, 10);
            Assert.AreEqual(1, arrivals);
            Assert.Less(PathFollower.HorizontalDistance(body.Position, replacement), 0.16f);

            body.Execute(new MoveCommand(new[] { body.Position + Vector3.forward * 10 }, MoveStyle.Run));
            yield return Until(() => body.CurrentSpeed > 2, 3);
            body.Stop();
            Vector3 stoppedAt = body.Position;
            yield return UntilElapsed(0.25f);
            Assert.Less(PathFollower.HorizontalDistance(body.Position, stoppedAt), 0.001f);
            Assert.IsFalse(body.HasArrived);
            body.Execute(default);
            body.Execute(new MoveCommand(new[] { Vector3.zero, new Vector3(float.NaN, 0, 1) }, MoveStyle.Run));
            Assert.IsFalse(body.IsMoving);
            Assert.IsFalse(body.HasArrived);
            Assert.AreEqual(1, arrivals);

            Vector3 final = body.Position + Vector3.right;
            Action nextCommand = null;
            nextCommand = () =>
            {
                body.Arrived -= nextCommand;
                body.Execute(new MoveCommand(new[] { final }, MoveStyle.Sneak));
            };
            body.Arrived += nextCommand;
            body.Execute(new MoveCommand(new[] { body.Position, body.Position }, MoveStyle.Walk));
            Assert.AreEqual(2, arrivals);
            Assert.IsTrue(body.IsMoving);
            Assert.IsFalse(body.HasArrived);
            yield return Until(() => body.HasArrived, 10);
            Assert.AreEqual(3, arrivals);
            body.Execute(MoveCommand.Hold());
            Assert.IsFalse(body.HasArrived);
            Assert.IsFalse(body.IsMoving);

            var lookAt = new GameObject("GV temporary strafe look target");
            lookAt.transform.position = body.Position + Vector3.forward * 5;
            body.Execute(new MoveCommand(new[] { body.Position + Vector3.right * 3 }, MoveStyle.Strafe, lookAt.transform));
            yield return Until(() => body.CurrentSpeed > 1.7f, 3);
            Assert.LessOrEqual(body.CurrentSpeed, 1.8001f);
            yield return Until(() => body.GetComponent<AgentRotation>().AngleTo(lookAt.transform.position - body.Position) < 8, 3);
            Assert.Greater(Vector3.Dot(body.transform.forward, (lookAt.transform.position - body.Position).normalized), 0.98f);
            body.Execute(MoveCommand.Hold(lookAt.transform));
            Assert.AreEqual(3, arrivals, "Holding cancels the strafe route, not an arrival.");
        }

        [UnityTest]
        public IEnumerator Runtime_NormalSlowArrivalDoesNotStuckButSolidObstacleDoes()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyStuck();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyStuck()
        {
            AgentBody body = CreateBody();
            Set(body, "slowingRadius", 8f);
            Set(body, "stuckTimeout", 0.2f);
            int stuck = 0;
            body.Stuck += () => stuck++;
            body.Execute(new MoveCommand(new[] { Vector3.forward * 2 }, MoveStyle.Walk));
            yield return Until(() => body.HasArrived || stuck > 0, 25);
            Assert.AreEqual(0, stuck, "Normal deceleration must still count as progress.");
            Assert.IsTrue(body.HasArrived);
            body.GetComponent<AgentMotor>().Warp(Vector3.zero);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 1, 1.2f);
            wall.transform.localScale = new Vector3(4, 2, 0.3f);
            Physics.SyncTransforms();
            body.Stuck += body.Stop;
            body.Execute(new MoveCommand(new[] { Vector3.forward * 3 }, MoveStyle.Run));
            yield return Until(() => stuck > 0, 10);
            Assert.AreEqual(1, stuck);
            Assert.Less(body.Position.z, 1);
            Assert.IsFalse(body.HasArrived);
            Assert.IsFalse(body.IsMoving);
        }

        [UnityTest]
        public IEnumerator BreacherActualMoveAction_PollsArrivalAndCancelsSharedBody()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/Manula/HunterSandbox.unity");
            // Disable only the test route driver in memory; no scene asset is changed.
            foreach (MonoBehaviour component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (component.GetType().Name == "HunterSandboxRouteDriver") component.enabled = false;
            yield return new EnterPlayMode();
            yield return VerifyBreacherAction();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyBreacherAction()
        {
            AgentBody body = Object.FindFirstObjectByType<AgentBody>();
            // Teammate runtime lives in Assembly-CSharp, which asmdef tests cannot reference.
            // Reflection keeps this scoped integration test from adding a runtime dependency.
            Type agentType = Type.GetType("BlackoutProtocol.AI.Breacher.BreacherAgent, Assembly-CSharp", true);
            var agent = (Behaviour)body.gameObject.AddComponent(agentType);
            agent.enabled = false; // Test Begin/Tick/Cancel only, not a fabricated configured GOAP scene.
            var destination = new GameObject("GV temporary door approach");
            destination.transform.position = new Vector3(6, 0, -3);
            agentType.GetField("doorApproach", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(agent, destination.transform);
            Type actionType = Type.GetType("BlackoutProtocol.AI.Breacher.Logic.MoveToAction, BlackoutProtocol.Breacher.Logic", true);
            Type worldType = Type.GetType("BlackoutProtocol.AI.Breacher.Logic.WorldState, BlackoutProtocol.Breacher.Logic", true);
            object empty = Activator.CreateInstance(worldType);
            object action = Activator.CreateInstance(actionType, "Door", empty, empty, empty, 1f);
            agentType.GetMethod("Begin").Invoke(agent, new[] { action });
            Assert.AreEqual(MoveStyle.Run, body.Style);
            Assert.AreEqual("Running", agentType.GetMethod("Tick").Invoke(agent, new[] { action }).ToString());
            yield return Until(() => body.HasArrived, 50);
            Assert.AreEqual("Succeeded", agentType.GetMethod("Tick").Invoke(agent, new[] { action }).ToString());
            Assert.Less(PathFollower.HorizontalDistance(body.Position, destination.transform.position), 0.16f);
            agentType.GetMethod("Cancel").Invoke(agent, new[] { action });
            Assert.IsFalse(body.IsMoving);
            Assert.IsFalse(body.HasArrived);
            Component mover = body.GetComponent(Type.GetType("BlackoutProtocol.AI.Breacher.BreacherPathMover, Assembly-CSharp", true));
            Assert.IsFalse((bool)mover.GetType().GetProperty("IsMoving").GetValue(mover));

            destination.transform.position = body.Position + Vector3.left * 3;
            agentType.GetMethod("Begin").Invoke(agent, new[] { action });
            Assert.IsTrue(body.IsMoving);
            Assert.AreEqual("Running", agentType.GetMethod("Tick").Invoke(agent, new[] { action }).ToString());
            agentType.GetMethod("Cancel").Invoke(agent, new[] { action });
            Assert.AreEqual("Failed", agentType.GetMethod("Tick").Invoke(agent, new[] { action }).ToString(),
                "A cancelled movement must not complete as an obsolete success.");
            Assert.AreEqual(Vector3.zero, body.Velocity);
            Vector3 cancelledAt = body.Position;
            yield return UntilElapsed(0.3f);
            Assert.Less(PathFollower.HorizontalDistance(body.Position, cancelledAt), 0.001f);
            Assert.IsFalse(body.HasArrived);
        }

        [UnityTest]
        public IEnumerator InterruptedStop_MovingAndNearArrival_ClearsIntentVelocityAndLaterEvents()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyInterruptedStop();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyInterruptedStop()
        {
            AgentBody body = CreateBody();
            Set(body, "stuckTimeout", 0.12f);
            int arrivals = 0, stuck = 0;
            body.Arrived += () => arrivals++;
            body.Stuck += () => stuck++;
            for (int phase = 0; phase < 2; phase++)
            {
                body.GetComponent<AgentMotor>().Warp(Vector3.zero);
                body.transform.rotation = Quaternion.identity;
                Vector3 goal = Vector3.forward * (phase == 0 ? 8 : 2);
                body.Execute(new MoveCommand(new[] { goal }, MoveStyle.Run));
                if (phase == 0) yield return Until(() => body.CurrentSpeed > 2, 3);
                else yield return Until(() => body.IsMoving && PathFollower.HorizontalDistance(body.Position, goal) < 0.4f, 8);
                Assert.Greater(body.Velocity.magnitude, 0);
                body.Stop();
                body.Stop(); // Idempotent; it must not rearm an obsolete destination/timer.
                Assert.IsFalse(body.IsMoving);
                Assert.IsFalse(body.HasArrived);
                Assert.AreEqual(MoveStyle.Idle, body.Style);
                Assert.AreEqual(0, body.Waypoints.Count);
                Assert.AreEqual(0, body.CurrentSpeed);
                Assert.AreEqual(Vector3.zero, body.Velocity);
                Vector3 stoppedAt = body.Position;
                yield return UntilElapsed(0.5f); // More than four stuck timeouts.
                Assert.Less(PathFollower.HorizontalDistance(body.Position, stoppedAt), 0.001f);
                Assert.AreEqual(0, arrivals);
                Assert.AreEqual(0, stuck);
            }
            body.Execute(new MoveCommand(new[] { body.Position + Vector3.forward * 3 }, MoveStyle.Run));
            yield return Until(() => body.CurrentSpeed > 0.5f, 3);
            body.Execute(new MoveCommand(new[] { body.Position, new Vector3(float.NaN, 0, 0) }, MoveStyle.Run));
            Assert.AreEqual(0, body.Waypoints.Count);
            Assert.AreEqual(Vector3.zero, body.Velocity);
            Assert.IsFalse(body.IsMoving);
            yield return UntilElapsed(0.3f);
            Assert.AreEqual(0, arrivals);
            Assert.AreEqual(0, stuck);
        }

        [UnityTest]
        public IEnumerator InterruptedReplacement_AccelerationSlowdownAndTurn_OnlyNewDestinationArrives()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyInterruptedReplacement();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyInterruptedReplacement()
        {
            AgentBody body = CreateBody();
            int arrivals = 0, stuck = 0;
            Vector3 arrivedAt = default;
            body.Arrived += () => { arrivals++; arrivedAt = body.Position; };
            body.Stuck += () => stuck++;
            var rotation = body.GetComponent<AgentRotation>();
            for (int phase = 0; phase < 3; phase++)
            {
                body.Stop();
                body.GetComponent<AgentMotor>().Warp(Vector3.zero);
                body.transform.rotation = Quaternion.identity;
                Vector3 oldGoal = Vector3.forward * (phase == 1 ? 2.5f : phase == 2 ? -4 : 8);
                body.Execute(new MoveCommand(new[] { oldGoal }, MoveStyle.Run));
                if (phase == 0)
                    yield return Until(() => body.CurrentSpeed > 0.5f && body.CurrentSpeed < 3, 3);
                else if (phase == 1)
                    yield return Until(() => body.IsMoving && PathFollower.HorizontalDistance(body.Position, oldGoal) < 0.4f && body.CurrentSpeed < 0.9f, 8);
                else
                {
                    rotation.TurnRate = 30;
                    yield return UntilElapsed(0.25f);
                    Assert.Greater(rotation.AngleTo(oldGoal - body.Position), 100);
                }
                Assert.AreEqual(phase, arrivals);
                Vector3 goal = body.Position + new Vector3(2.5f, 0, 0.5f);
                float oldSpeed = body.CurrentSpeed;
                body.Execute(new MoveCommand(new[] { body.Position, goal }, MoveStyle.Walk));
                Assert.IsFalse(body.HasArrived);
                Assert.IsTrue(body.IsMoving);
                Assert.LessOrEqual(body.CurrentSpeed, oldSpeed + 0.001f);
                Assert.AreEqual(goal, body.Waypoints[body.Waypoints.Count - 1]);
                rotation.TurnRate = 360;
                yield return Until(() => body.HasArrived, 10);
                Assert.AreEqual(phase + 1, arrivals);
                Assert.Less(PathFollower.HorizontalDistance(arrivedAt, goal), 0.16f);
            }
            Assert.AreEqual(0, stuck);
        }

        [UnityTest]
        public IEnumerator ContinuousBlockage_NotifiesOnceThenRearmsAfterProgressAndNewRouteRecovers()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyContinuousBlockage();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyContinuousBlockage()
        {
            AgentBody body = CreateBody();
            Set(body, "stuckTimeout", 0.15f);
            GameObject wall = MakeWall(1.2f);
            int stuck = 0, arrivals = 0;
            body.Stuck += () => stuck++;
            body.Arrived += () => arrivals++;
            body.Execute(new MoveCommand(new[] { Vector3.forward * 5 }, MoveStyle.Run));
            yield return Until(() => stuck == 1, 5);
            yield return UntilElapsed(0.7f);
            Assert.AreEqual(1, stuck, "Unhandled continuous blockage must not repeat on each timeout.");
            Assert.AreEqual(0, arrivals);
            Assert.IsTrue(body.IsMoving, "A Stuck notification does not itself choose a brain recovery action.");
            wall.SetActive(false);
            Physics.SyncTransforms();
            yield return Until(() => body.Position.z > 1.2f, 3);
            wall.transform.position = new Vector3(0, 1, 2.5f);
            wall.SetActive(true);
            Physics.SyncTransforms();
            yield return Until(() => stuck == 2, 5);
            yield return UntilElapsed(0.5f);
            Assert.AreEqual(2, stuck, "Progress permits one notification for a new blockage episode.");
            Vector3 recovery = body.Position + new Vector3(3, 0, -2);
            Assert.Less(body.Velocity.magnitude, 0.1f);
            body.Execute(new MoveCommand(new[] { recovery }, MoveStyle.Walk));
            Assert.Less(body.CurrentSpeed, 0.1f, "Do not inherit the unrealized cruise speed of a blocked controller.");
            yield return Until(() => body.HasArrived, 12);
            Assert.AreEqual(1, arrivals);
            Assert.AreEqual(2, stuck);
            Assert.Less(PathFollower.HorizontalDistance(body.Position, recovery), 0.16f);
        }

        [UnityTest]
        public IEnumerator RealStuckAndArrivalCallbacks_ReentrantExecuteAndStopOwnTheFinalState()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyReentrantCallbacks();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyReentrantCallbacks()
        {
            AgentBody body = CreateBody();
            Set(body, "stuckTimeout", 0.15f);
            MakeWall(1.2f);
            int stuck = 0, arrivals = 0;
            body.Stuck += () =>
            {
                stuck++;
                body.Execute(new MoveCommand(new[] { body.Position + new Vector3(3, 0, -1) }, MoveStyle.Walk));
                Assert.IsTrue(body.IsMoving);
                Assert.IsFalse(body.HasArrived);
            };
            body.Arrived += () =>
            {
                arrivals++;
                Assert.AreEqual(Vector3.zero, body.Velocity, "Arrival observers must see rest, not the previous frame's motion.");
                if (arrivals == 1)
                    body.Execute(new MoveCommand(new[] { body.Position + Vector3.back * 2 }, MoveStyle.Sneak));
                else body.Stop();
            };
            body.Execute(new MoveCommand(new[] { Vector3.forward * 4 }, MoveStyle.Run));
            yield return Until(() => arrivals == 2, 18);
            Assert.AreEqual(1, stuck);
            Assert.IsFalse(body.HasArrived, "Stop inside Arrived must not be overwritten by old completion bookkeeping.");
            Assert.IsFalse(body.IsMoving);
            Assert.AreEqual(0, body.Waypoints.Count);
            yield return UntilElapsed(0.5f);
            Assert.AreEqual(2, arrivals);
            Assert.AreEqual(1, stuck);
        }

        [UnityTest]
        public IEnumerator VerySlowCornerArrivalAndTurnInPlace_AreProgressNotBlockage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifySlowCornerAndTurn();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifySlowCornerAndTurn()
        {
            AgentBody body = CreateBody();
            Set(body, "cornerSpeed", 0.05f);
            Set(body, "slowingRadius", 4f);
            Set(body, "stuckTimeout", 0.1f);
            body.GetComponent<AgentRotation>().TurnRate = 45;
            body.transform.rotation = Quaternion.Euler(0, 180, 0);
            int stuck = 0;
            body.Stuck += () => stuck++;
            body.Execute(new MoveCommand(new[] { Vector3.zero, Vector3.forward * 2, new Vector3(2, 0, 2) }, MoveStyle.Sneak));
            yield return UntilElapsed(0.5f);
            Assert.IsTrue(body.IsMoving);
            Assert.Less(PathFollower.HorizontalDistance(body.Position, Vector3.zero), 0.01f);
            Assert.AreEqual(0, stuck);
            yield return Until(() => body.HasArrived, 25);
            Assert.AreEqual(0, stuck);
        }

        [UnityTest]
        public IEnumerator HunterSound_ReplacesActiveRouteWithoutObsoleteArrivalThenArrivesAndSearches()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/Manula/HunterNoiseSandbox.unity");
            yield return new EnterPlayMode();
            yield return VerifyHunterSoundReplacement();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyHunterSoundReplacement()
        {
            yield return null;
            AgentBody body = Object.FindFirstObjectByType<AgentBody>();
            Component brain = body.GetComponent(Type.GetType("BlackoutProtocol.AI.Hunter.HunterBrain, BlackoutProtocol.Hunter.Runtime", true));
            Type type = brain.GetType();
            int Arrivals() => (int)type.GetProperty("ArrivalCount").GetValue(brain);
            int Requests() => (int)type.GetProperty("PathRequestCount").GetValue(brain);
            Vector3 Destination() => (Vector3)type.GetProperty("Destination").GetValue(brain);
            string ActionName() => type.GetProperty("CurrentAction").GetValue(brain).ToString();
            Vector3 oldGoal = new Vector3(6, 0, -3);
            GameEvents.RaiseNoise(new NoiseEvent(oldGoal, NoiseLevels.Alarm, NoiseType.Alarm, Time.time));
            yield return Until(() => ActionName() == "Investigate" && body.IsMoving, 4);
            int previousRequests = Requests(), previousArrivals = Arrivals();
            Vector3 newGoal = new Vector3(-6, 0, -0.5f);
            GameEvents.RaiseNoise(new NoiseEvent(newGoal, 100, NoiseType.Alarm, Time.time));
            yield return Until(() => Requests() > previousRequests && PathFollower.HorizontalDistance(Destination(), newGoal) < 0.1f && body.IsMoving, 4);
            Assert.AreEqual(previousArrivals, Arrivals());
            yield return Until(() => Arrivals() > previousArrivals, 15);
            Assert.Less(PathFollower.HorizontalDistance(body.Position, newGoal), 0.3f);
            yield return Until(() => ActionName() == "Search", 3);
            Assert.AreEqual(previousArrivals + 1, Arrivals());
        }

        private static GameObject MakeWall(float z)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 1, z);
            wall.transform.localScale = new Vector3(4, 2, 0.3f);
            Physics.SyncTransforms();
            return wall;
        }

        private static AgentBody CreateBody()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -0.25f, 0);
            floor.transform.localScale = new Vector3(30, 0.5f, 30);
            var go = new GameObject("GV steering test body");
            var body = go.AddComponent<AgentBody>();
            var controller = go.GetComponent<CharacterController>();
            controller.center = Vector3.up;
            controller.height = 2;
            controller.radius = 0.25f;
            controller.minMoveDistance = 0;
            Physics.SyncTransforms();
            return body;
        }

        private static void Set(Object target, string name, float value)
        {
            var settings = new SerializedObject(target);
            settings.FindProperty(name).floatValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerator Until(Func<bool> condition, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), "Timed out waiting for movement integration.");
        }

        private static IEnumerator UntilElapsed(float seconds)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline) yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
