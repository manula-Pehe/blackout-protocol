using BlackoutProtocol.Body;
using UnityEngine;

namespace BlackoutProtocol.AI.Hunter.Testing
{
    /// <summary>
    /// Presentation and explicit test signals only. The existing brain owns decisions,
    /// the pathfinder owns routes and AgentBody owns all robot motion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManulaTechnicalDemo : MonoBehaviour
    {
        public enum Control { Alarm, Gunshot, Impact, RepeatedDecoy, ShowTarget, HideTarget, Pause, Resume, Follow, Overview }

        [SerializeField] private HunterBrain hunter;
        [SerializeField] private HunterSandboxNoiseTrigger signals;
        [SerializeField] private AgentAnimatorDriver animationDriver;
        [SerializeField] private Camera viewCamera;
        private AgentBody body;
        private AgentMotor motor;
        private bool valid, follow = true, cameraReady;
        private float nextRefresh;
        private string actionText = "Waiting for Hunter", motionText = "Waiting for movement", signalText = "Silence";
        private GUIStyle titleStyle, subtitleStyle, actionStyle, detailStyle, buttonStyle;
        private static readonly string[] Labels = {
            "Alarm across wall", "Gunshot nearby", "Single impact", "Repeat decoy x5", "Show simulated target", "Hide simulated target",
            "Pause / Idle", "Resume Hunter", "Follow Titan", "Overview / route"
        };

        public HunterBrain Hunter => hunter;
        public HunterSandboxNoiseTrigger Signals => signals;
        public AgentAnimatorDriver AnimationDriver => animationDriver;
        public Camera ViewCamera => viewCamera;
        public bool Paused => valid && !hunter.enabled;
        public bool Following => follow;
        public int ControlsExecuted { get; private set; }
        public Control LastControl { get; private set; }

        private void Awake()
        {
            body = hunter != null ? hunter.GetComponent<AgentBody>() : null;
            motor = body != null ? body.GetComponent<AgentMotor>() : null;
            valid = hunter != null && signals != null && signals.TestTarget != null && animationDriver != null &&
                animationDriver.GetComponent<AgentBody>() == body && viewCamera != null && motor != null;
            if (!valid)
            {
                Debug.LogError("ManulaTechnicalDemo: assign the Hunter, its animation driver, noise controls, simulated target and camera.", this);
                enabled = false;
            }
        }

        /// <summary>The same command entry point is used by visible buttons and integration tests.</summary>
        public void ExecuteControl(Control control)
        {
            if (!valid || control < Control.Alarm || control > Control.Overview) return;
            if (Paused && control <= Control.ShowTarget) return;
            switch (control)
            {
                case Control.Alarm: signals.EmitAlarmAcrossWall(); break;
                case Control.Gunshot: signals.EmitNearbyGunshot(); break;
                case Control.Impact: signals.EmitSingleImpact(); break;
                case Control.RepeatedDecoy: signals.EmitRepeatedImpacts(); break;
                case Control.ShowTarget: signals.ShowTarget(); break;
                case Control.HideTarget: signals.HideTarget(); break;
                case Control.Pause: hunter.enabled = false; body.Stop(); break;
                case Control.Resume: hunter.enabled = true; break;
                case Control.Follow: follow = true; cameraReady = false; break;
                case Control.Overview: follow = false; cameraReady = false; break;
            }
            ControlsExecuted++;
            LastControl = control;
            nextRefresh = 0;
        }

        public static float PresentationScale(float width, float height) => Mathf.Max(0.1f, Mathf.Min(width / 1280f, height / 800f));

        /// <summary>Actual screen-pixel button bounds, also useful for graphical click verification.</summary>
        public static Rect GetControlRect(Control control, float width, float height)
        {
            float s = PresentationScale(width, height), w = width / s, h = height / s;
            int index = (int)control;
            float columns = index < 6 ? 6 : 4;
            float column = index < 6 ? index : index - 6;
            float cell = (w - 48) / columns;
            return new Rect((24 + column * cell) * s, (h - (index < 6 ? 144 : 108)) * s, (cell - 6) * s, 28 * s);
        }

        private void LateUpdate()
        {
            if (!valid) return;
            float s = PresentationScale(Screen.width, Screen.height);
            // Reserve the left score panel, top title and bottom controls in the Game view.
            viewCamera.rect = new Rect(416 * s / Screen.width, 184 * s / Screen.height,
                Mathf.Max(0.1f, 1 - 432 * s / Screen.width), Mathf.Max(0.1f, 1 - 370 * s / Screen.height));
            viewCamera.orthographic = !follow;
            viewCamera.orthographicSize = 10.5f;
            viewCamera.fieldOfView = 38;
            Vector3 focus = follow ? body.Position + Vector3.up * 0.9f : new Vector3(0, 0, -0.5f);
            Vector3 position = follow ? body.Position + new Vector3(body.Position.x < 0 ? -5 : 5, 5.5f, -6) : new Vector3(0, 22, -16);
            float blend = cameraReady ? 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime) : 1;
            viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, position, blend);
            Quaternion facing = Quaternion.LookRotation(focus - viewCamera.transform.position);
            viewCamera.transform.rotation = Quaternion.Slerp(viewCamera.transform.rotation, facing, blend);
            cameraReady = true;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;
            actionText = hunter.CurrentAction.ToString().ToUpperInvariant() + (Paused ? "  /  PAUSED" : "  /  AUTONOMOUS");
            motionText = $"{body.Style} | actual {animationDriver.ActualHorizontalSpeed:F2} m/s | Animator Speed {animationDriver.SmoothedSpeedParameter:F2}\n" +
                $"{animationDriver.Status} | root motion OFF | {(hunter.HasActiveRoute ? "following A*" : "stopped / waiting")} | waypoint {body.CurrentWaypointIndex}/{body.Waypoints.Count}\n" +
                $"arrivals {hunter.ArrivalCount} | routes {hunter.PathRequestCount} | stuck recoveries {hunter.StuckRecoveryCount} | grounded {motor.IsGrounded}";
            signalText = signals.LastEmission;
        }

        private void OnGUI()
        {
            if (!valid) return;
            if (titleStyle == null)
            {
                titleStyle = Style(26, new Color(0.94f, 0.76f, 0.27f), true);
                subtitleStyle = Style(14, new Color(0.7f, 0.82f, 0.91f));
                actionStyle = Style(23, new Color(0.3f, 0.92f, 0.92f), true);
                detailStyle = Style(13, Color.white);
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            }
            float scale = PresentationScale(Screen.width, Screen.height), w = Screen.width / scale, h = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            try
            {
                // These pixels lie outside the camera viewport. Opaque backing
                // prevents old IMGUI labels accumulating between rendered frames.
                Color previousColor = GUI.color;
                GUI.color = new Color(0.025f, 0.04f, 0.065f, 1);
                GUI.DrawTexture(new Rect(416, 0, w - 416, 186), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, h - 184, w, 184), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(w - 16, 186, 16, h - 370), Texture2D.whiteTexture);
                GUI.color = previousColor;
                GUI.Box(new Rect(416, 16, w - 432, 160), GUIContent.none);
                GUI.Label(new Rect(430, 24, w - 460, 34), "BLACKOUT PROTOCOL / MANULA", titleStyle);
                GUI.Label(new Rect(430, 60, w - 460, 21), "GV Agent Controller  +  IS Hunter Utility AI  |  technical demonstration", subtitleStyle);
                GUI.Label(new Rect(430, 84, w - 460, 31), actionText, actionStyle);
                GUI.Label(new Rect(430, 119, w - 460, 54), motionText, detailStyle);
                GUI.Box(new Rect(16, h - 174, w - 32, 158), GUIContent.none);
                GUI.Label(new Rect(24, h - 171, w - 48, 23), "TEST SIGNALS / shared NoiseEvent inputs, never forced AI decisions  |  F1: score panel", subtitleStyle);
                for (Control control = Control.Alarm; control <= Control.Overview; control++)
                {
                    Rect pixels = GetControlRect(control, Screen.width, Screen.height);
                    Rect local = new Rect(pixels.x / scale, pixels.y / scale, pixels.width / scale, pixels.height / scale);
                    bool enabledBefore = GUI.enabled;
                    GUI.enabled = !Paused || control >= Control.HideTarget;
                    if (GUI.Button(local, Labels[(int)control], buttonStyle)) ExecuteControl(control);
                    GUI.enabled = enabledBefore;
                }
                GUI.Label(new Rect(24, h - 76, w - 48, 21), signalText, detailStyle);
                GUI.Label(new Rect(24, h - 55, w - 48, 20), "Alarm: Investigate | gunshot: Chase | let the trail finish: Search then Wander | simulated target: Attack decision (no attack clip).", detailStyle);
                GUI.Label(new Rect(24, h - 34, w - 48, 20), "Separate GV grounding/separation: stop Play, then open AgentBodyGroundingSeparationSandbox.unity in this folder.", detailStyle);
            }
            finally { GUI.matrix = previous; }
        }

        private static GUIStyle Style(int size, Color color, bool bold = false)
        {
            var result = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = false, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
            result.normal.textColor = color;
            return result;
        }
    }
}
