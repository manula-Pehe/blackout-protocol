using System;
using BlackoutProtocol.Core;
using BlackoutProtocol.GameFlow.Logic;
using BlackoutProtocol.Interaction;
using BlackoutProtocol.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BlackoutProtocol.GameFlow
{
    /// <summary>Adapts the objective state to existing player/console contracts without changing navigation or AI.</summary>
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] private PlayerState player;
        [SerializeField] private ShutdownConsole console = null;
        [SerializeField] private Transform consoleMarker = null;
        [SerializeField] private Transform exitMarker = null;
        [SerializeField] private TextMesh exitLabel = null;
        [SerializeField, HideInInspector] private string setupVersion = string.Empty;
        private PlayerHealth health;
        private PlayerInteractor interactor;
        private GameFlowState state;
        private Behaviour[] controls;
        private bool[] enabledBefore;
        private bool frozen;
        private float previousTimeScale;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private int terminalFrame;

        public GameFlowState State => state;
        public string SetupVersion => setupVersion;
        public string InteractionPrompt => !state.IsTerminal && interactor != null ? interactor.CurrentPrompt : string.Empty;
        public string Objective => state.Phase == GameFlowPhase.ReachCore ? "REACH THE CONTROL CORE" :
            state.Phase == GameFlowPhase.ActivateConsole ? "ACTIVATE THE SHUTDOWN CONSOLE  [E]" :
            state.Phase == GameFlowPhase.ExitUnlocked ? "EXIT UNLOCKED — REACH THE ELEVATOR" :
            state.Phase == GameFlowPhase.Won ? "EXTRACTION COMPLETE — YOU WIN" : "SIGNAL LOST — YOU DIED";

        private void Awake() { state = new GameFlowState(); }

        private void OnEnable()
        {
            if (console == null || consoleMarker == null || exitMarker == null || exitLabel == null)
            {
                Debug.LogError("Game Flow is missing setup references. Run Setup Game Flow in Main.", this);
                enabled = false;
                return;
            }
            state.Changed += PhaseChanged;
            console.Activated += ConsoleActivated;
            PlayerRegistry.CurrentChanged += PlayerChanged;
            BindPlayer(player != null ? player : PlayerRegistry.Current as PlayerState);
            RefreshExit();
            if (state.IsTerminal) FreezeControls();
        }

        private void OnDisable()
        {
            if (state != null) state.Changed -= PhaseChanged;
            if (console != null) console.Activated -= ConsoleActivated;
            PlayerRegistry.CurrentChanged -= PlayerChanged;
            if (health != null) health.Died -= PlayerDied;
            RestoreControls();
        }

        private void PlayerChanged(IPlayerState current)
        {
            if (!state.IsTerminal) BindPlayer(current as PlayerState);
        }

        private void BindPlayer(PlayerState current)
        {
            if (health != null) health.Died -= PlayerDied;
            player = current;
            health = player != null ? player.GetComponent<PlayerHealth>() : null;
            interactor = player != null ? player.GetComponent<PlayerInteractor>() : null;
            if (health != null) health.Died += PlayerDied;
        }

        private void LateUpdate()
        {
            if (state.IsTerminal)
            {
                if (Time.frameCount > terminalFrame &&
                    ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
                    (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame))) Restart();
                return;
            }
            if (player == null || health == null) return;
            if (health.IsDead) { state.Die(); return; }
            Vector3 position = player.transform.position;
            if (InsideCore(position)) state.ReachCore();
            if (new Bounds(exitMarker.position + Vector3.up, new Vector3(1.4f, 4f, 2.4f)).Contains(position))
                state.Extract(!health.IsDead);
        }

        private static bool InsideCore(Vector3 position) => new Bounds(new Vector3(28f, 1f, 0f), new Vector3(20f, 6f, 24f)).Contains(position);

        private void ConsoleActivated(GameObject user)
        {
            if (player == null || health == null || user != player.gameObject || state.IsTerminal) return;
            if (health.IsDead) { state.Die(); return; }
            if (!InsideCore(player.transform.position)) return;
            state.ReachCore();
            state.ActivateConsole();
        }

        private void PlayerDied(GameObject source) { state.Die(); }

        private void PhaseChanged(GameFlowPhase phase)
        {
            RefreshExit();
            if (state.IsTerminal) FreezeControls();
        }

        private void RefreshExit()
        {
            if (exitLabel == null) return;
            bool unlocked = state.Phase == GameFlowPhase.ExitUnlocked || state.Phase == GameFlowPhase.Won;
            exitLabel.text = unlocked ? "ELEVATOR EXIT\nUNLOCKED" : "ELEVATOR EXIT\nLOCKED";
            exitLabel.color = unlocked ? Color.green : new Color(1f, 0.55f, 0.15f);
        }

        private void FreezeControls()
        {
            if (frozen) return;
            frozen = true;
            terminalFrame = Time.frameCount;
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            if (player != null)
            {
                controls = new Behaviour[] { player.GetComponent<PlayerInput>(), player.GetComponent<FirstPersonController>(),
                    player.GetComponent<PlayerLook>(), player.GetComponent<PlayerWeapon>(), interactor,
                    player.GetComponent<PlayerHold>(), player.GetComponent<FootstepEmitter>() };
                enabledBefore = new bool[controls.Length];
                for (int index = 0; index < controls.Length; index++)
                {
                    if (controls[index] == null) continue;
                    enabledBefore[index] = controls[index].enabled;
                    controls[index].enabled = false;
                }
            }
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreControls()
        {
            if (!frozen) return;
            frozen = false;
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            if (controls == null) return;
            for (int index = 0; index < controls.Length; index++)
                if (controls[index] != null) controls[index].enabled = enabledBefore[index];
        }

        /// <summary>Reloads the saved scene, rebuilding all transient objective, player and enemy state.</summary>
        public void Restart()
        {
            if (!state.RequestRestart()) return;
            string path = gameObject.scene.path;
            if (string.IsNullOrEmpty(path) || !Application.CanStreamedLevelBeLoaded(path))
            {
                state.RestartFailed();
                Debug.LogError("Cannot restart: save Main and enable it in Build Settings. The result remains paused.", this);
                return;
            }
            try
            {
                Time.timeScale = previousTimeScale;
                SceneManager.LoadScene(path, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                Time.timeScale = 0f;
                state.RestartFailed();
                Debug.LogError("Restart failed: " + exception.Message, this);
            }
        }
    }
}
