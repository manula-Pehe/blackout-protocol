using BlackoutProtocol.Core;
using BlackoutProtocol.GameFlow;
using BlackoutProtocol.GameFlow.Logic;
using UnityEngine;

namespace BlackoutProtocol.Audio
{
    /// <summary>Plays ambience and responds to alarm notifications without emitting gameplay events.</summary>
    [DisallowMultipleComponent]
    public sealed class AudioPresenter : MonoBehaviour
    {
        [SerializeField] private AudioClip ambienceClip;
        [SerializeField] private AudioClip alarmClip;
        [SerializeField] private AudioClip confirmationClip;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.08f;
        [SerializeField, Range(0f, 1f)] private float alarmVolume = 0.18f;
        [SerializeField, Range(0f, 1f)] private float oneShotVolume = 0.35f;

        private AudioSource ambienceSource;
        private AudioSource alarmSource;
        private AudioSource oneShotSource;
        private GameFlowState flowState;
        private bool shutdownConfirmed;

        private void Awake() => EnsureSources();

        private void OnEnable()
        {
            GameEvents.AlarmRaised -= HandleAlarmRaised;
            GameEvents.AlarmRaised += HandleAlarmRaised;
            BindGameFlow();
        }

        private void Start()
        {
            // All scene Awake calls have completed, including Game Flow's state initialization.
            BindGameFlow();
            StartAmbience();
        }

        private void HandleAlarmRaised(Vector3 playerLastSeenPosition) => StartAlarm();

        private void BindGameFlow()
        {
            if (flowState != null) return;
            foreach (GameFlowController flow in FindObjectsByType<GameFlowController>(FindObjectsSortMode.None))
            {
                if (flow.gameObject.scene != gameObject.scene || !flow.isActiveAndEnabled || flow.State == null) continue;
                flowState = flow.State;
                flowState.Changed += HandleFlowPhaseChanged;
                // Re-enabling after shutdown must not restore alarm playback or repeat confirmation.
                if (flowState.Phase == GameFlowPhase.ExitUnlocked || flowState.Phase == GameFlowPhase.Won)
                    HandleFlowPhaseChanged(GameFlowPhase.ExitUnlocked);
                break;
            }
        }

        private void HandleFlowPhaseChanged(GameFlowPhase phase)
        {
            if (phase != GameFlowPhase.ExitUnlocked || shutdownConfirmed) return;
            shutdownConfirmed = true;
            StopAlarm();
            PlayConfirmation();
        }

        private void EnsureSources()
        {
            if (ambienceSource == null) ambienceSource = CreateSource("Ambience", true);
            if (alarmSource == null) alarmSource = CreateSource("Alarm", true);
            if (oneShotSource == null) oneShotSource = CreateSource("One Shots", false);
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            return source;
        }

        private bool CanPlay => Application.isPlaying && isActiveAndEnabled;

        [ContextMenu("Audio/Start Ambience (Play Mode)")]
        public void StartAmbience()
        {
            if (!CanPlay || ambienceClip == null) return;
            EnsureSources();
            StartLoop(ambienceSource, ambienceClip, ambienceVolume);
        }

        [ContextMenu("Audio/Stop Ambience")]
        public void StopAmbience()
        {
            if (ambienceSource != null) ambienceSource.Stop();
        }

        [ContextMenu("Audio/Start Alarm (Play Mode)")]
        public void StartAlarm()
        {
            if (!CanPlay || shutdownConfirmed || alarmClip == null) return;
            EnsureSources();
            StartLoop(alarmSource, alarmClip, alarmVolume);
        }

        [ContextMenu("Audio/Stop Alarm")]
        public void StopAlarm()
        {
            if (alarmSource != null) alarmSource.Stop();
        }

        private static void StartLoop(AudioSource source, AudioClip clip, float volume)
        {
            source.volume = Mathf.Clamp01(volume);
            if (source.clip == clip && source.isPlaying) return;
            source.clip = clip;
            source.Play();
        }

        [ContextMenu("Audio/Play Confirmation (Play Mode)")]
        public void PlayConfirmation()
        {
            if (!CanPlay || confirmationClip == null) return;
            EnsureSources();
            oneShotSource.volume = Mathf.Clamp01(oneShotVolume);
            oneShotSource.PlayOneShot(confirmationClip);
        }

        public void StopOneShots()
        {
            if (oneShotSource != null) oneShotSource.Stop();
        }

        [ContextMenu("Audio/Stop All")]
        public void StopAll()
        {
            StopAmbience();
            StopAlarm();
            StopOneShots();
        }

        private void OnDisable()
        {
            GameEvents.AlarmRaised -= HandleAlarmRaised;
            if (flowState != null) flowState.Changed -= HandleFlowPhaseChanged;
            flowState = null;
            StopAll();
        }
    }
}
