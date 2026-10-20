using System.Collections;
using BlackoutProtocol.AI.Hunter.Testing;
using BlackoutProtocol.Body;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.AI.Hunter.Tests
{
    public sealed class HunterSandboxTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/Manula/HunterSandbox.unity";

        [UnityTest]
        public IEnumerator SavedSandbox_ReopensAndHunterWalksAroundCornerToGoal()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Assert.That(Object.FindFirstObjectByType<NavMeshSurface>().navMeshData, Is.Not.Null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null; // Awake/Start send the A* route to AgentBody.

            var driver = Object.FindFirstObjectByType<HunterSandboxRouteDriver>();
            var body = driver.GetComponent<AgentBody>();
            var pathfinder = driver.GetComponent<HunterNavMeshPathfinder>();
            Assert.That(Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(driver.State, Is.EqualTo(HunterSandboxRouteDriver.RouteState.Following), driver.Details);
            Assert.That(pathfinder.LastSearch.Succeeded, Is.True);
            Assert.That(pathfinder.LastSearch.NodePath.Count, Is.GreaterThan(1));
            Assert.That(pathfinder.GraphNodeCount, Is.GreaterThan(0));
            Assert.That(driver.Waypoints.Count, Is.GreaterThan(2));
            Assert.That(NavMesh.Raycast(body.Position, driver.Destination.position, out _, NavMesh.AllAreas), Is.True,
                "The obstacle must block a direct walk to the goal.");

            float highestZ = body.Position.z;
            float deadline = Time.realtimeSinceStartup + 50;
            while (driver.State == HunterSandboxRouteDriver.RouteState.Following && Time.realtimeSinceStartup < deadline)
            {
                highestZ = Mathf.Max(highestZ, body.Position.z);
                if (Mathf.Abs(body.Position.x) < 0.85f)
                    Assert.That(body.Position.z, Is.GreaterThan(2.25f), "Robot crossed through the obstacle instead of around it.");
                yield return null;
            }
            Assert.That(driver.State, Is.EqualTo(HunterSandboxRouteDriver.RouteState.Arrived), driver.Details);
            Assert.That(body.HasArrived, Is.True);
            Assert.That(highestZ, Is.GreaterThan(2.25f));
            Vector3 difference = body.Position - driver.Destination.position;
            difference.y = 0;
            Assert.That(difference.magnitude, Is.LessThan(0.2f));
            Debug.Log($"Hunter sandbox verification: ARRIVED, position={body.Position}, detour maxZ={highestZ:F3}, waypoints={driver.Waypoints.Count}, graph nodes={pathfinder.GraphNodeCount}.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RouteDriver_ReportsMissingReferencesMissingBakeAndUnreachableGoal()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            var driver = Object.FindFirstObjectByType<HunterSandboxRouteDriver>();
            driver.Destination.position = new Vector3(1000, 0, 1000);
            LogAssert.Expect(LogType.Error, "Hunter sandbox: Hunter A* could not calculate a route. Check that start and destination are on the same connected baked NavMesh; rebake after geometry changes.");
            Assert.That(driver.BeginRoute(), Is.False);
            Assert.That(driver.State, Is.EqualTo(HunterSandboxRouteDriver.RouteState.Failed));
            Assert.That(driver.GetComponent<AgentBody>().IsMoving, Is.False);

            NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
            surface.RemoveData();
            surface.navMeshData = null;
            LogAssert.Expect(LogType.Error, "Hunter sandbox: Missing baked NavMesh data. Open HunterSandbox and use Tools > Hunter > Bake Open Hunter Sandbox, then save the scene and its NavMesh asset.");
            Assert.That(driver.BeginRoute(), Is.False);

            var incomplete = new GameObject("Incomplete Hunter Test").AddComponent<HunterSandboxRouteDriver>();
            incomplete.enabled = false;
            LogAssert.Expect(LogType.Error, "Hunter sandbox: Missing component references. Assign AgentBody, HunterNavMeshPathfinder, Destination and Navigation Surface on the route driver.");
            Assert.That(incomplete.BeginRoute(), Is.False);
            Assert.That(incomplete.State, Is.EqualTo(HunterSandboxRouteDriver.RouteState.Failed));
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
