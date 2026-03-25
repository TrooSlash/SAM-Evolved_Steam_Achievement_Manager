using NUnit.Framework;
using SAM.Picker;

namespace SAM.Tests
{
    [TestFixture]
    public class PlaytimeFormattingTests
    {
        [TestCase(0, "—")]
        [TestCase(-1, "—")]
        [TestCase(-100, "—")]
        [TestCase(1, "1 min")]
        [TestCase(30, "30 min")]
        [TestCase(59, "59 min")]
        [TestCase(60, "1.0 hrs")]
        [TestCase(90, "1.5 hrs")]
        [TestCase(120, "2.0 hrs")]
        [TestCase(150, "2.5 hrs")]
        [TestCase(1000, "16.7 hrs")]
        public void FormatPlaytime_ReturnsExpected(int minutes, string expected)
        {
            Assert.AreEqual(expected, PlaytimeReader.FormatPlaytime(minutes));
        }

        [Test]
        public void FormatLastPlayed_Zero_ReturnsDash()
        {
            Assert.AreEqual("—", PlaytimeReader.FormatLastPlayed(0));
        }

        [Test]
        public void FormatLastPlayed_Negative_ReturnsDash()
        {
            Assert.AreEqual("—", PlaytimeReader.FormatLastPlayed(-1));
        }

        [Test]
        public void FormatLastPlayed_VeryOld_ReturnsFullDate()
        {
            // 2020-01-01 00:00:00 UTC = 1577836800
            string result = PlaytimeReader.FormatLastPlayed(1577836800);
            Assert.That(result, Does.Contain("2020"));
        }
    }
}
