using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>Speed envelopes only. Never changes a route or invents a navigation shortcut.</summary>
    public static class SteeringMath
    {
        /// <summary>Linear arrival curve based on distance along the remaining polyline, not through obstacles.</summary>
        public static float ArrivalSpeed(float cruiseSpeed, float remainingDistance, float slowingRadius)
        {
            if (!Finite(cruiseSpeed) || !Finite(remainingDistance) || !Finite(slowingRadius)) return 0;
            return Mathf.Max(0, cruiseSpeed) * Mathf.Clamp01(remainingDistance / Mathf.Max(0.01f, slowingRadius));
        }

        /// <summary>
        /// v² = terminal² + 2*a*d, with one step of braking reserve:
        /// v*dt + (v²-terminal²)/(2*a) &lt;= d. The corner's terminal speed is a floor,
        /// not a minimum locomotion speed. Actual acceleration remains the body's MoveTowards limit.
        /// </summary>
        public static float ApproachSpeed(float distance, float terminalSpeed, float acceleration, float deltaTime)
        {
            if (!Finite(distance) || !Finite(terminalSpeed) || !Finite(acceleration) || !Finite(deltaTime)) return 0;
            float a = Mathf.Max(0.01f, acceleration);
            float terminal = Mathf.Max(0, terminalSpeed);
            float reserve = a * Mathf.Max(0, deltaTime);
            return Mathf.Max(terminal, Mathf.Sqrt(reserve * reserve + terminal * terminal +
                2 * a * Mathf.Max(0, distance)) - reserve);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
