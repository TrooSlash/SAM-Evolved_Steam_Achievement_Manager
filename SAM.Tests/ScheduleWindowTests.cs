using NUnit.Framework;
using SAM.Picker;

namespace SAM.Tests
{
    [TestFixture]
    public class ScheduleWindowTests
    {
        // Normal range: 09:00 - 17:00 (540 - 1020)
        [TestCase(540, 540, 1020, true, TestName = "ExactStart")]
        [TestCase(1020, 540, 1020, true, TestName = "ExactEnd")]
        [TestCase(780, 540, 1020, true, TestName = "Middle")]
        [TestCase(539, 540, 1020, false, TestName = "BeforeStart")]
        [TestCase(1021, 540, 1020, false, TestName = "AfterEnd")]
        [TestCase(0, 540, 1020, false, TestName = "Midnight")]
        // Overnight range: 22:00 - 06:00 (1320 - 360)
        [TestCase(1320, 1320, 360, true, TestName = "OvernightExactStart")]
        [TestCase(360, 1320, 360, true, TestName = "OvernightExactEnd")]
        [TestCase(0, 1320, 360, true, TestName = "OvernightMidnight")]
        [TestCase(1400, 1320, 360, true, TestName = "OvernightLateNight")]
        [TestCase(200, 1320, 360, true, TestName = "OvernightEarlyMorning")]
        [TestCase(720, 1320, 360, false, TestName = "OvernightMidday")]
        [TestCase(600, 1320, 360, false, TestName = "OvernightMorning")]
        // Full day: 00:00 - 23:59 (0 - 1439)
        [TestCase(0, 0, 1439, true, TestName = "FullDayStart")]
        [TestCase(720, 0, 1439, true, TestName = "FullDayMiddle")]
        [TestCase(1439, 0, 1439, true, TestName = "FullDayEnd")]
        // Same start and end: 12:00 - 12:00 (720 - 720)
        [TestCase(720, 720, 720, true, TestName = "SameStartEnd_Exact")]
        [TestCase(0, 720, 720, false, TestName = "SameStartEnd_Other")]
        public void IsWithinSchedule_ReturnsExpected(
            int nowMin, int startMin, int endMin, bool expected)
        {
            bool result = ActiveGamesForm.IsWithinSchedule(nowMin, startMin, endMin);
            Assert.AreEqual(expected, result);
        }
    }
}
