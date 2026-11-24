using System;
namespace BlackoutProtocol.AI.Hunter.Logic
{
    /// <summary>Route searches remain event-driven but never exceed four per scaled game second.</summary>
    public static class HunterRouteThrottle
    {
        public const float MinimumInterval = 0.25f;
        public static float NextAllowed(float now, float requestedInterval) => now + Math.Max(MinimumInterval, requestedInterval);
        public static float AfterInvalidation(float now, float existingDeadline) => Math.Max(now, existingDeadline);
    }
}
