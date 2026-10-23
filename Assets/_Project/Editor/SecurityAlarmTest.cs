using BlackoutProtocol.Core;
using UnityEditor;
using UnityEngine;

namespace BlackoutProtocol.Editor
{
    public static class SecurityAlarmTest
    {
        private const string MenuPath = "Tools/Blackout Protocol/Test Security Alarm";

        [MenuItem(MenuPath)]
        private static void RaiseAlarm()
        {
            if (!EditorApplication.isPlaying) return;

            Vector3 position = PlayerRegistry.Current != null
                ? PlayerRegistry.Current.Position
                : Vector3.zero;
            GameEvents.RaiseAlarm(position);
            Debug.Log("[Security Alarm Test] Raised AlarmRaised. Existing gameplay subscribers also receive this event.");
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRaiseAlarm() => EditorApplication.isPlaying;
    }
}
