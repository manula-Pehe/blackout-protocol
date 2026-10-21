using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>Pure horizontal encounter scoring. No path search, random choice or physics mutation.</summary>
    public static class SeparationMath
    {
        public static float Threat(Vector3 forward, Vector3 offset, float clearance, float horizon)
        {
            forward.y = offset.y = 0;
            if (!Finite(forward) || !Finite(offset) || !Finite(clearance) || !Finite(horizon) ||
                forward.sqrMagnitude < 0.0001f || clearance <= 0 || horizon <= clearance) return 0;
            forward.Normalize();
            float ahead = Vector3.Dot(offset, forward);
            float lateral = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward)));
            if (ahead < 0 || ahead > horizon) return 0;
            return Mathf.Clamp01(1 - Mathf.Max(0, ahead - clearance) / (horizon - clearance)) *
                   Mathf.Clamp01(1 - lateral / (clearance + 0.2f));
        }

        /// <summary>Head-on ties pass on each agent's right; parallel/coincident ties use stable caller keys.</summary>
        public static float PassingSide(Vector3 forward, Vector3 offset, Vector3 otherForward, int selfKey, int otherKey)
        {
            forward.y = offset.y = otherForward.y = 0;
            float lateral = Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward.normalized));
            if (Mathf.Abs(lateral) > 0.05f) return -Mathf.Sign(lateral);
            if (Vector3.Dot(forward.normalized, otherForward.normalized) < -0.25f) return 1;
            return selfKey < otherKey ? 1 : -1;
        }

        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
