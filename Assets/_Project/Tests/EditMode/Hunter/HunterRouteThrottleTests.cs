using NUnit.Framework;
using BlackoutProtocol.AI.Hunter.Logic;
namespace BlackoutProtocol.AI.Hunter.Tests
{
    public sealed class HunterRouteThrottleTests
    {
        [TestCase(0.01f, 10.25f)]
        [TestCase(0.25f, 10.25f)]
        [TestCase(0.75f, 10.75f)]
        public void RouteIntervalNeverFallsBelowFourHz(float interval, float expected)
        {
            Assert.That(HunterRouteThrottle.NextAllowed(10f, interval), Is.EqualTo(expected).Within(0.00001f));
        }
        [Test]
        public void LevelChangeCannotBypassPendingCooldown()
        {
            float deadline = HunterRouteThrottle.NextAllowed(10f, 0.25f);
            Assert.That(HunterRouteThrottle.AfterInvalidation(10.1f, deadline), Is.EqualTo(deadline));
            Assert.That(HunterRouteThrottle.AfterInvalidation(11f, deadline), Is.EqualTo(11f));
        }
    }
}
