using BlackoutProtocol.AI.Hunter.Logic;
using NUnit.Framework;

namespace BlackoutProtocol.AI.Hunter.Tests
{
    public sealed class HunterUtilitySelectorTests
    {
        private readonly HunterUtilitySelector selector =
            new HunterUtilitySelector(hysteresisBonus: 0f);

        [Test]
        public void Silence_SelectsWander()
        {
            HunterUtilitySelection result = selector.Select(Context());

            Assert.That(result.Action, Is.EqualTo(HunterAction.Wander));
            Assert.That(result.Scores.Wander, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void StrongTrustedDistantNoise_SelectsInvestigate()
        {
            HunterUtilitySelection result = selector.Select(Context(
                soundIntensity: 1f,
                noiseTrust: 1f,
                targetProximity: 0.1f));

            Assert.That(result.Action, Is.EqualTo(HunterAction.Investigate));
            Assert.That(result.Scores.Investigate, Is.GreaterThan(result.Scores.Chase));
        }

        [Test]
        public void ConfidentNearbyTrail_SelectsChase()
        {
            HunterUtilitySelection result = selector.Select(Context(
                soundIntensity: 0.8f,
                memoryConfidence: 1f,
                noiseTrust: 1f,
                targetProximity: 0.8f));

            Assert.That(result.Action, Is.EqualTo(HunterAction.Chase));
        }

        [Test]
        public void LostTrailWithUrgency_SelectsSearch()
        {
            HunterUtilitySelection result = selector.Select(Context(
                memoryConfidence: 0.7f,
                noiseTrust: 1f,
                searchUrgency: 1f));

            Assert.That(result.Action, Is.EqualTo(HunterAction.Search));
        }

        [Test]
        public void EligibleNearbyTarget_SelectsAttack()
        {
            HunterUtilitySelection result = selector.Select(Context(
                memoryConfidence: 1f,
                noiseTrust: 1f,
                targetProximity: 1f,
                attackEligibility: 1f));

            Assert.That(result.Action, Is.EqualTo(HunterAction.Attack));
            Assert.That(result.Score, Is.EqualTo(1f));
        }

        [Test]
        public void StrongUntrustedNoise_DoesNotSelectNoiseDrivenAction()
        {
            HunterUtilitySelection result = selector.Select(Context(
                soundIntensity: 1f,
                noiseTrust: 0f));

            Assert.That(result.Action, Is.EqualTo(HunterAction.Wander));
            Assert.That(result.Scores.Investigate, Is.Zero);
            Assert.That(result.Scores.Chase, Is.Zero);
        }

        [Test]
        public void Hysteresis_RetainsCurrentActionWhenScoresAreClose()
        {
            var context = Context(
                soundIntensity: 0.5f,
                noiseTrust: 1f,
                targetProximity: 0.5f);
            var selectorWithHysteresis = new HunterUtilitySelector(0.08f);

            HunterUtilitySelection withoutCurrent = selectorWithHysteresis.Select(context);
            HunterUtilitySelection withCurrent = selectorWithHysteresis.Select(
                context,
                HunterAction.Wander);

            Assert.That(withoutCurrent.Action, Is.EqualTo(HunterAction.Investigate));
            Assert.That(withCurrent.Action, Is.EqualTo(HunterAction.Wander));
            Assert.That(
                withCurrent.Scores.Wander,
                Is.EqualTo(withCurrent.BaseScores.Wander + 0.08f).Within(0.0001f));
        }

        [Test]
        public void Hysteresis_DoesNotHideClearlySuperiorAction()
        {
            var selectorWithHysteresis = new HunterUtilitySelector(0.08f);

            HunterUtilitySelection result = selectorWithHysteresis.Select(
                Context(
                    targetProximity: 1f,
                    attackEligibility: 1f),
                HunterAction.Wander);

            Assert.That(result.Action, Is.EqualTo(HunterAction.Attack));
        }

        [Test]
        public void ExactTie_UsesFixedThreatPriority()
        {
            HunterUtilitySelection result = selector.Select(Context(
                soundIntensity: 1f,
                noiseTrust: 1f,
                targetProximity: 1f,
                attackEligibility: 0.65f));

            Assert.That(result.Scores.Attack, Is.EqualTo(result.Scores.Investigate));
            Assert.That(result.Action, Is.EqualTo(HunterAction.Attack));
        }

        [Test]
        public void InvalidInputs_AreNormalizedAndProduceFiniteScores()
        {
            var context = new HunterUtilityContext(
                float.NaN,
                float.NegativeInfinity,
                float.PositiveInfinity,
                -4f,
                8f,
                2f);

            HunterUtilitySelection result = selector.Select(context);

            Assert.That(context.SoundIntensity, Is.Zero);
            Assert.That(context.MemoryConfidence, Is.Zero);
            Assert.That(context.NoiseTrust, Is.EqualTo(1f));
            Assert.That(context.TargetProximity, Is.Zero);
            Assert.That(context.SearchUrgency, Is.EqualTo(1f));
            Assert.That(context.AttackEligibility, Is.EqualTo(1f));
            AssertScoresAreNormalized(result.BaseScores);
        }

        [Test]
        public void InvalidCurrentAction_IsIgnoredSafely()
        {
            var context = Context(soundIntensity: 1f, noiseTrust: 1f);

            HunterUtilitySelection normal = selector.Select(context);
            HunterUtilitySelection invalidCurrent = selector.Select(
                context,
                (HunterAction)999);

            Assert.That(invalidCurrent.Action, Is.EqualTo(normal.Action));
            Assert.That(invalidCurrent.Score, Is.EqualTo(normal.Score));
        }

        [Test]
        public void Scores_AreExposedForEveryAction()
        {
            HunterUtilitySelection result = selector.Select(Context(
                soundIntensity: 0.4f,
                memoryConfidence: 0.6f,
                noiseTrust: 0.8f,
                targetProximity: 0.5f,
                searchUrgency: 0.3f,
                attackEligibility: 0.2f));

            Assert.That(result.Scores.Get(HunterAction.Wander), Is.EqualTo(result.Scores.Wander));
            Assert.That(result.Scores.Get(HunterAction.Investigate), Is.EqualTo(result.Scores.Investigate));
            Assert.That(result.Scores.Get(HunterAction.Chase), Is.EqualTo(result.Scores.Chase));
            Assert.That(result.Scores.Get(HunterAction.Search), Is.EqualTo(result.Scores.Search));
            Assert.That(result.Scores.Get(HunterAction.Attack), Is.EqualTo(result.Scores.Attack));
        }

        private static HunterUtilityContext Context(
            float soundIntensity = 0f,
            float memoryConfidence = 0f,
            float noiseTrust = 0f,
            float targetProximity = 0f,
            float searchUrgency = 0f,
            float attackEligibility = 0f)
        {
            return new HunterUtilityContext(
                soundIntensity,
                memoryConfidence,
                noiseTrust,
                targetProximity,
                searchUrgency,
                attackEligibility);
        }

        private static void AssertScoresAreNormalized(HunterUtilityScores scores)
        {
            Assert.That(scores.Wander, Is.InRange(0f, 1f));
            Assert.That(scores.Investigate, Is.InRange(0f, 1f));
            Assert.That(scores.Chase, Is.InRange(0f, 1f));
            Assert.That(scores.Search, Is.InRange(0f, 1f));
            Assert.That(scores.Attack, Is.InRange(0f, 1f));
        }
    }
}
