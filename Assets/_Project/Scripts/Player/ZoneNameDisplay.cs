using UnityEngine;

namespace BlackoutProtocol.Player
{
    /// <summary>Screen-space room identification; world geometry can never reveal another room's label.</summary>
    [DisallowMultipleComponent]
    public sealed class ZoneNameDisplay : MonoBehaviour
    {
        private GUIStyle style;

        private void OnGUI()
        {
            Vector3 position = transform.position;
            string zone = position.z > 21f ? "03  MAINTENANCE / STORAGE" :
                position.x < -13f ? "01  LOADING BAY" :
                position.x > 20f ? "04  CONTROL CORE" : "02  SECURITY HUB";
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                style.normal.textColor = new Color(0.45f, 0.9f, 0.95f);
            }
            GUI.Label(new Rect(Screen.width - 360f, 16f, 340f, 32f), zone, style);
        }
    }
}
