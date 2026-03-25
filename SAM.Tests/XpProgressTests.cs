using NUnit.Framework;
using SAM.Picker;

namespace SAM.Tests
{
    [TestFixture]
    public class XpProgressTests
    {
        [Test]
        public void HalfwayThrough_Returns50Percent()
        {
            // xp=150, neededCurrentLevel=100, neededToLevelUp=50
            // earned this level = 150 - 100 = 50
            // total for level = 50 + 50 = 100
            // progress = 50/100 = 0.5
            float result = ProfilePanel.CalculateXpProgress(150, 100, 50);
            Assert.AreEqual(0.5f, result, 0.001f);
        }

        [Test]
        public void JustStartedLevel_ReturnsZero()
        {
            // xp=100, neededCurrentLevel=100, neededToLevelUp=100
            // earned = 0, total = 100, progress = 0
            float result = ProfilePanel.CalculateXpProgress(100, 100, 100);
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void AlmostDone_ReturnsNearOne()
        {
            // xp=199, neededCurrentLevel=100, neededToLevelUp=1
            // earned = 99, total = 100, progress = 0.99
            float result = ProfilePanel.CalculateXpProgress(199, 100, 1);
            Assert.AreEqual(0.99f, result, 0.001f);
        }

        [Test]
        public void ZeroXpNeededCurrentLevel_ReturnsZero()
        {
            // When xpNeededCurrentLevel is 0, formula returns 0
            float result = ProfilePanel.CalculateXpProgress(50, 0, 100);
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void ZeroXpNeeded_FullProgress()
        {
            // xp=200, neededCurrentLevel=100, neededToLevelUp=0
            // earned = 100, total = 100, progress = 1.0
            float result = ProfilePanel.CalculateXpProgress(200, 100, 0);
            Assert.AreEqual(1f, result, 0.001f);
        }

        [Test]
        public void AllZeros_ReturnsZero()
        {
            float result = ProfilePanel.CalculateXpProgress(0, 0, 0);
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void NegativeProgress_ClampedToZero()
        {
            // xp=50, neededCurrentLevel=100, neededToLevelUp=50
            // earned = 50-100 = -50, total = 0, progress clamped to 0
            float result = ProfilePanel.CalculateXpProgress(50, 100, 50);
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void ProgressAlwaysBetweenZeroAndOne()
        {
            // Test a range of values
            for (int xp = 0; xp <= 500; xp += 50)
            for (int cur = 0; cur <= 300; cur += 100)
            for (int needed = 0; needed <= 200; needed += 50)
            {
                float result = ProfilePanel.CalculateXpProgress(xp, cur, needed);
                Assert.That(result, Is.GreaterThanOrEqualTo(0f),
                    $"xp={xp}, cur={cur}, needed={needed}");
                Assert.That(result, Is.LessThanOrEqualTo(1f),
                    $"xp={xp}, cur={cur}, needed={needed}");
            }
        }
    }
}
