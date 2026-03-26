using NUnit.Framework;
using SAM.Picker;

namespace SAM.Tests
{
    [TestFixture]
    public class PluralTests
    {
        [SetUp]
        public void SetUp()
        {
            Localization.Current = Localization.Language.Russian;
        }

        [TearDown]
        public void TearDown()
        {
            Localization.Current = Localization.Language.English;
        }

        // Russian: one form (last digit 1, not teens)
        [TestCase(1, "игра", TestName = "RU_1_One")]
        [TestCase(21, "игра", TestName = "RU_21_One")]
        [TestCase(31, "игра", TestName = "RU_31_One")]
        [TestCase(101, "игра", TestName = "RU_101_One")]
        [TestCase(121, "игра", TestName = "RU_121_One")]
        // Russian: few form (last digit 2-4, not teens)
        [TestCase(2, "игры", TestName = "RU_2_Few")]
        [TestCase(3, "игры", TestName = "RU_3_Few")]
        [TestCase(4, "игры", TestName = "RU_4_Few")]
        [TestCase(22, "игры", TestName = "RU_22_Few")]
        [TestCase(34, "игры", TestName = "RU_34_Few")]
        // Russian: many form (0, 5-20, x5-x9, teens)
        [TestCase(0, "игр", TestName = "RU_0_Many")]
        [TestCase(5, "игр", TestName = "RU_5_Many")]
        [TestCase(10, "игр", TestName = "RU_10_Many")]
        [TestCase(11, "игр", TestName = "RU_11_Many")]
        [TestCase(12, "игр", TestName = "RU_12_Many")]
        [TestCase(14, "игр", TestName = "RU_14_Many")]
        [TestCase(19, "игр", TestName = "RU_19_Many")]
        [TestCase(20, "игр", TestName = "RU_20_Many")]
        [TestCase(25, "игр", TestName = "RU_25_Many")]
        [TestCase(100, "игр", TestName = "RU_100_Many")]
        [TestCase(111, "игр", TestName = "RU_111_Many")]
        [TestCase(112, "игр", TestName = "RU_112_Many")]
        public void RussianPlural(int count, string expected)
        {
            Assert.AreEqual(expected, Localization.Plural(count, "игра", "игры", "игр"));
        }

        [Test]
        public void EnglishPlural_One()
        {
            Localization.Current = Localization.Language.English;
            Assert.AreEqual("game", Localization.Plural(1, "game", "games", "games"));
        }

        [Test]
        public void EnglishPlural_Many()
        {
            Localization.Current = Localization.Language.English;
            Assert.AreEqual("games", Localization.Plural(0, "game", "games", "games"));
            Assert.AreEqual("games", Localization.Plural(2, "game", "games", "games"));
            Assert.AreEqual("games", Localization.Plural(5, "game", "games", "games"));
            Assert.AreEqual("games", Localization.Plural(21, "game", "games", "games"));
        }

        [Test]
        public void RussianPlural_NegativeNumbers()
        {
            Assert.AreEqual("игра", Localization.Plural(-1, "игра", "игры", "игр"));
            Assert.AreEqual("игры", Localization.Plural(-2, "игра", "игры", "игр"));
            Assert.AreEqual("игр", Localization.Plural(-5, "игра", "игры", "игр"));
        }
    }
}
