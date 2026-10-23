using System;

namespace BlackoutProtocol.AI.Flanker.Logic
{
    /// <summary>Engine-independent navigation and memory coordinates.</summary>
    public readonly struct FlankerPoint
    {
        public FlankerPoint(float horizontal, float height, float depth)
        {
            if (!Finite(horizontal) || !Finite(height) || !Finite(depth)) throw new ArgumentException("Coordinates must be finite.");
            X = horizontal;
            Y = height;
            Z = depth;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static double Distance(FlankerPoint first, FlankerPoint second)
        {
            double horizontal = (double)first.X - second.X;
            double height = (double)first.Y - second.Y;
            double depth = (double)first.Z - second.Z;
            return Math.Sqrt(horizontal * horizontal + height * height + depth * depth);
        }
    }

    /// <summary>Finite, monotonic health; damage after death is ignored.</summary>
    public sealed class FlankerVitals
    {
        public FlankerVitals(float maximum)
        {
            if (!FlankerPoint.Finite(maximum) || maximum <= 0f) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = maximum;
            Current = maximum;
        }

        public float Maximum { get; }
        public float Current { get; private set; }
        public float Normalized => Current / Maximum;
        public bool IsDead => Current <= 0f;

        public bool Damage(float amount)
        {
            if (IsDead || !FlankerPoint.Finite(amount) || amount <= 0f) return false;
            Current = Math.Max(0f, Current - amount);
            return true;
        }
    }

    /// <summary>Last-known evidence with caller-controlled time and explicit expiration.</summary>
    public sealed class FlankerMemory
    {
        private bool remembered;
        private double expiresAt;
        public FlankerPoint Position { get; private set; }

        public void Remember(FlankerPoint position, double now, double lifetime)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(lifetime) || double.IsInfinity(lifetime) || lifetime <= 0d) return;
            Position = position;
            expiresAt = now + lifetime;
            remembered = true;
        }

        public bool HasEvidence(double now) => remembered && !double.IsNaN(now) && now < expiresAt;
        public void Clear() => remembered = false;
    }

    /// <summary>Pure cover scoring; negative infinity means the candidate is unusable.</summary>
    public static class FlankerCoverScoring
    {
        public static double Score(bool reachable, bool available, float protection, double routeLength,
            float facingDot, float threatDistance, bool retreat)
        {
            if (!reachable || !available || !FlankerPoint.Finite(protection) || !FlankerPoint.Finite(facingDot) ||
                !FlankerPoint.Finite(threatDistance) || double.IsNaN(routeLength) || double.IsInfinity(routeLength) ||
                routeLength < 0d || protection <= 0f || threatDistance < 3f) return double.NegativeInfinity;
            if (!retreat && facingDot > 0.5f) return double.NegativeInfinity;
            double protectionScore = Math.Min(1f, protection) * 6d;
            return protectionScore + (retreat ? Math.Min(30f, threatDistance) * 0.25d : (1d - facingDot) * 2d) - routeLength * 0.12d;
        }
    }

    /// <summary>Preserves an in-flight segment so repeated centroid anchoring cannot pull an agent backwards.</summary>
    public static class FlankerRoutePolicy
    {
        public static bool KeepCurrent(bool commanded, bool moving, float goalDisplacement, bool nearCorner, double now, double earliestReplan)
        {
            return commanded && moving && (goalDisplacement < 1.5f || !nearCorner || now < earliestReplan);
        }
    }
}
