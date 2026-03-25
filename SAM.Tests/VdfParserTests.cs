using NUnit.Framework;
using SAM.Picker;

namespace SAM.Tests
{
    [TestFixture]
    public class VdfParserTests
    {
        [Test]
        public void ParseVdf_SingleApp_ReturnsPlaytime()
        {
            string vdf = @"
""UserLocalConfigStore""
{
    ""Software""
    {
        ""Valve""
        {
            ""Steam""
            {
                ""apps""
                {
                    ""730""
                    {
                        ""Playtime""		""120""
                        ""LastPlayed""		""1711900000""
                    }
                }
            }
        }
    }
}";
            var result = PlaytimeReader.ParseVdf(vdf);
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey(730));
            Assert.AreEqual(120, result[730].PlaytimeMinutes);
            Assert.AreEqual(1711900000L, result[730].LastPlayedTimestamp);
        }

        [Test]
        public void ParseVdf_MultipleApps_ReturnsAll()
        {
            string vdf = @"
""apps""
{
    ""730""
    {
        ""Playtime""		""120""
        ""LastPlayed""		""1711900000""
    }
    ""440""
    {
        ""Playtime""		""3600""
        ""LastPlayed""		""1711800000""
    }
}";
            var result = PlaytimeReader.ParseVdf(vdf);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(120, result[730].PlaytimeMinutes);
            Assert.AreEqual(3600, result[440].PlaytimeMinutes);
        }

        [Test]
        public void ParseVdf_PlaytimeForever_Recognized()
        {
            string vdf = @"
""apps""
{
    ""730""
    {
        ""playtime_forever""		""999""
    }
}";
            var result = PlaytimeReader.ParseVdf(vdf);
            Assert.AreEqual(999, result[730].PlaytimeMinutes);
        }

        [Test]
        public void ParseVdf_NoAppsSection_ReturnsEmpty()
        {
            string vdf = @"""SomeOtherSection"" { }";
            var result = PlaytimeReader.ParseVdf(vdf);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseVdf_EmptyApps_ReturnsEmpty()
        {
            string vdf = @"""apps"" { }";
            var result = PlaytimeReader.ParseVdf(vdf);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseVdf_NestedSubkeys_SkipsCorrectly()
        {
            string vdf = @"
""apps""
{
    ""730""
    {
        ""Playtime""		""50""
        ""cloud""
        {
            ""someSetting""		""1""
        }
    }
}";
            var result = PlaytimeReader.ParseVdf(vdf);
            Assert.AreEqual(50, result[730].PlaytimeMinutes);
        }

        [Test]
        public void ParseVdf_ZeroPlaytime_IncludedBecauseNonNegative()
        {
            string vdf = @"
""apps""
{
    ""730""
    {
        ""Playtime""		""0""
    }
}";
            var result = PlaytimeReader.ParseVdf(vdf);
            // PlaytimeMinutes=0 satisfies (>= 0) condition, so entry is included
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(0, result[730].PlaytimeMinutes);
        }
    }
}
