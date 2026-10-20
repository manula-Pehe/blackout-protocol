using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>Testable locomotion-parameter math, independent of an Animator, rig or AI brain.</summary>
    public static class LocomotionAnimation
    {
        public static float HorizontalSpeed(Vector3 velocity)
        {
            if (!Finite(velocity.x) || !Finite(velocity.z)) return 0;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            return Finite(speed) ? speed : 0;
        }

        /// <summary>Reference 1 gives m/s; reference equal to the authored run speed gives Run = 1.</summary>
        public static float TargetParameter(float speed, float referenceSpeed, float idleThreshold)
        {
            if (!Finite(speed) || speed <= 0) return 0;
            float threshold = Finite(idleThreshold) ? Mathf.Max(0, idleThreshold) : 0;
            if (speed <= threshold) return 0;
            float reference = Finite(referenceSpeed) && referenceSpeed >= 0.01f ? referenceSpeed : 1;
            float result = speed / reference;
            return Finite(result) ? result : 0;
        }

        /// <summary>Exponential response: value += (target - value) * (1 - exp(-dt/tau)).</summary>
        public static float Smooth(float current, float target, float dampTime, float deltaTime)
        {
            current = Finite(current) ? Mathf.Max(0, current) : 0;
            target = Finite(target) ? Mathf.Max(0, target) : 0;
            if (!Finite(deltaTime) || deltaTime <= 0) return current;
            if (!Finite(dampTime) || dampTime <= 0) return target;
            float value = Mathf.Lerp(current, target, 1 - Mathf.Exp(-deltaTime / dampTime));
            return Mathf.Abs(value - target) < 0.0001f ? target : value;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
