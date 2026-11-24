using System;
using System.Collections;
using BlackoutProtocol.AI.Hunter.Logic;
using BlackoutProtocol.Body;
using BlackoutProtocol.Core;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.AI.Hunter.Tests
{
    public sealed class HunterMainIntegrationTests
    {
        private const string MainScene = "Assets/_Project/Scenes/Main.unity";

        [UnityTest]
        public IEnumerator Main_HunterHearsNearbyPlayerNoiseAnimatesAndRebuildsAfterRealDoorChange()
        {
            EditorSceneManager.OpenScene(MainScene);
            HunterBrain editBrain = Object.FindFirstObjectByType<HunterBrain>();
            Assert.That(editBrain, Is.Not.Null);
            Assert.That(editBrain.GetComponent<AgentAnimatorDriver>(), Is.Not.Null,
                "Main Hunter must use the shared actual-speed animation driver.");
            Assert.That(editBrain.GetComponent<HunterDebugPanel>(), Is.Not.Null,
                "Main Hunter must expose its existing F1 diagnostics.");

            yield return new EnterPlayMode();
            yield return VerifyMainRuntime();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyMainRuntime()
        {
            yield return null;
            HunterBrain brain = Object.FindFirstObjectByType<HunterBrain>(FindObjectsInactive.Include);
            Assert.That(brain, Is.Not.Null, "Main Hunter must survive scene entry.");
            Assert.That(brain.gameObject.activeInHierarchy && brain.isActiveAndEnabled, Is.True);
            AgentAnimatorDriver driver = brain.GetComponent<AgentAnimatorDriver>();
            yield return WaitFor(() => driver.IsAnimatorReady && driver.ActualHorizontalSpeed > 0.05f, 12, brain);

            IPlayerState player = PlayerRegistry.Current;
            Assert.That(player, Is.Not.Null);
            Assert.That(NavMesh.SamplePosition(brain.transform.position + Vector3.forward * 5f,
                out NavMeshHit nearbyHit, 8f, NavMesh.AllAreas), Is.True);
            Vector3 nearby = nearbyHit.position;
            player.Transform.position = nearby;
            Physics.SyncTransforms();
            int accepted = brain.AcceptedNoiseCount;
            GameEvents.RaiseNoise(new NoiseEvent(nearby, NoiseLevels.Gunshot, NoiseType.Gunshot,
                Time.time, player.Transform.gameObject));
            Assert.That(brain.AcceptedNoiseCount, Is.EqualTo(accepted + 1));
            yield return WaitFor(() => brain.CurrentAction == HunterAction.Chase ||
                brain.CurrentAction == HunterAction.Investigate, 4, brain);
            if (Environment.GetEnvironmentVariable("HUNTER_COMPLETION_CAPTURE") == "1")
            {
                MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    string typeName = behaviours[i].GetType().Name;
                    if (typeName == "PlayerLook" || typeName == "BreacherAgent") behaviours[i].enabled = false;
                }
                Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++) cameras[i].enabled = false;
                Camera camera = new GameObject("Hunter completion evidence camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
                camera.rect = new Rect(0.45f, 0f, 0.55f, 1f);
                camera.transform.position = brain.transform.position + new Vector3(0f, 2.2f, -4f);
                camera.transform.rotation = Quaternion.LookRotation(
                    brain.transform.position + Vector3.up * 1f - camera.transform.position);
                Light light = new GameObject("Hunter completion evidence light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.transform.rotation = Quaternion.Euler(45f, 160f, 0f);
                brain.GetComponent<HunterDebugPanel>().ToggleOverlay();
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("/tmp/hunter-completion-main.png");
                yield return new WaitForEndOfFrame();
            }

            IDoor door = FindDoor();
            Assert.That(door, Is.Not.Null, "Main must contain its real sliding door contract.");
            HunterNavMeshPathfinder pathfinder = brain.GetComponent<HunterNavMeshPathfinder>();
            int changes = brain.NavigationChangeCount;
            int builds = pathfinder.GraphBuildCount;
            door.Open();
            yield return WaitFor(() => door.IsOpen, 5, brain);
            yield return WaitFor(() => brain.NavigationChangeCount > changes &&
                pathfinder.GraphBuildCount > builds, 5, brain);
        }

        private static IDoor FindDoor()
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.InstanceID);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IDoor door) return door;
            return null;
        }

        private static IEnumerator WaitFor(Func<bool> predicate, float timeout, HunterBrain brain)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(predicate(), Is.True,
                $"Main integration timeout: action={brain.CurrentAction}, status={brain.Status}, event={brain.LastRuntimeEvent}.");
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
