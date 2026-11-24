using BlackoutProtocol.AI.Sentinel;
using BlackoutProtocol.Body;
using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackoutProtocol.Audio
{
    /// <summary>Spatial movement and action sounds for all four robot types.</summary>
    [DisallowMultipleComponent]
    public sealed class RobotAudioFeedback : MonoBehaviour
    {
        private AgentBody groundBody;
        private SentinelDroneBody droneBody;
        private AudioSource oneShot;
        private AudioSource loop;
        private float nextStep;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInFirstScene() => Install();

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install();

        private static void Install()
        {
            foreach (AgentBody body in Object.FindObjectsByType<AgentBody>(FindObjectsSortMode.None))
                if (body.GetComponent<RobotAudioFeedback>() == null)
                    body.gameObject.AddComponent<RobotAudioFeedback>();
            foreach (SentinelDroneBody body in Object.FindObjectsByType<SentinelDroneBody>(FindObjectsSortMode.None))
                if (body.GetComponent<RobotAudioFeedback>() == null)
                    body.gameObject.AddComponent<RobotAudioFeedback>();
        }

        private void Awake()
        {
            groundBody = GetComponent<AgentBody>();
            droneBody = GetComponent<SentinelDroneBody>();
            oneShot = gameObject.AddComponent<AudioSource>();
            Configure(oneShot, 0.68f);
            loop = gameObject.AddComponent<AudioSource>();
            Configure(loop, 0.38f);
            loop.loop = true;
            if (droneBody != null) loop.clip = ProceduralSfx.DroneHum;
        }

        private static void Configure(AudioSource source, float volume)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = 22f;
            source.volume = volume;
        }

        private void OnEnable()
        {
            if (groundBody != null) groundBody.ActionRequested += OnAction;
            if (droneBody != null)
            {
                droneBody.ActionRequested += OnAction;
                droneBody.Died += OnDroneDied;
            }
        }

        private void OnDisable()
        {
            if (groundBody != null) groundBody.ActionRequested -= OnAction;
            if (droneBody != null)
            {
                droneBody.ActionRequested -= OnAction;
                droneBody.Died -= OnDroneDied;
            }
            if (loop != null) loop.Stop();
        }

        private void Update()
        {
            if (droneBody != null)
            {
                if (droneBody.IsShotDown) { loop.Stop(); return; }
                loop.pitch = droneBody.IsMoving ? 1.18f : 0.9f;
                if (!loop.isPlaying) loop.Play();
                return;
            }
            if (groundBody == null || !groundBody.IsMoving || Time.time < nextStep) return;
            nextStep = Time.time + (groundBody.Style == MoveStyle.Run ? 0.31f : 0.52f);
            oneShot.PlayOneShot(ProceduralSfx.Step, 0.45f);
        }

        private void OnAction(AgentAction action)
        {
            AudioClip clip = action == AgentAction.Attack ? ProceduralSfx.Attack :
                action == AgentAction.Hit ? ProceduralSfx.Hit :
                action == AgentAction.Die ? ProceduralSfx.Death : ProceduralSfx.Alert;
            oneShot.PlayOneShot(clip, 0.75f);
            if (action == AgentAction.Die) loop.Stop();
        }

        private void OnDroneDied() => loop.Stop();
    }
}
