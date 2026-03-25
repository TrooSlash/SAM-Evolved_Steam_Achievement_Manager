using NUnit.Framework;
using SAM.Game.Stats;

namespace SAM.Tests
{
    [TestFixture]
    public class PermissionFlagsTests
    {
        [Test]
        public void None_IsZero()
        {
            Assert.AreEqual(0, (int)StatFlags.None);
        }

        [Test]
        public void IncrementOnly_IsBit0()
        {
            Assert.AreEqual(1, (int)StatFlags.IncrementOnly);
        }

        [Test]
        public void Protected_IsBit1()
        {
            Assert.AreEqual(2, (int)StatFlags.Protected);
        }

        [Test]
        public void UnknownPermission_IsBit2()
        {
            Assert.AreEqual(4, (int)StatFlags.UnknownPermission);
        }

        [Test]
        public void PermissionValue2_MapsToProtected()
        {
            // This mirrors the logic in StatInfo.Extra:
            // flags |= ((Permission & 2) != 0) ? StatFlags.Protected : 0;
            int permission = 2;
            var flags = StatFlags.None;
            flags |= ((permission & 2) != 0) ? StatFlags.Protected : 0;
            flags |= ((permission & ~2) != 0) ? StatFlags.UnknownPermission : 0;

            Assert.IsTrue(flags.HasFlag(StatFlags.Protected));
            Assert.IsFalse(flags.HasFlag(StatFlags.UnknownPermission));
        }

        [Test]
        public void PermissionValue0_MapsToNone()
        {
            int permission = 0;
            var flags = StatFlags.None;
            flags |= ((permission & 2) != 0) ? StatFlags.Protected : 0;
            flags |= ((permission & ~2) != 0) ? StatFlags.UnknownPermission : 0;

            Assert.AreEqual(StatFlags.None, flags);
        }

        [Test]
        public void PermissionValue3_MapsToProtectedAndUnknown()
        {
            // bit 1 (Protected) + bit 0 (unknown non-2 bit)
            int permission = 3;
            var flags = StatFlags.None;
            flags |= ((permission & 2) != 0) ? StatFlags.Protected : 0;
            flags |= ((permission & ~2) != 0) ? StatFlags.UnknownPermission : 0;

            Assert.IsTrue(flags.HasFlag(StatFlags.Protected));
            Assert.IsTrue(flags.HasFlag(StatFlags.UnknownPermission));
        }

        [Test]
        public void PermissionValue4_MapsToUnknownOnly()
        {
            int permission = 4;
            var flags = StatFlags.None;
            flags |= ((permission & 2) != 0) ? StatFlags.Protected : 0;
            flags |= ((permission & ~2) != 0) ? StatFlags.UnknownPermission : 0;

            Assert.IsFalse(flags.HasFlag(StatFlags.Protected));
            Assert.IsTrue(flags.HasFlag(StatFlags.UnknownPermission));
        }

        [Test]
        public void FlagsCombine_Correctly()
        {
            var flags = StatFlags.IncrementOnly | StatFlags.Protected;
            Assert.AreEqual(3, (int)flags);
            Assert.IsTrue(flags.HasFlag(StatFlags.IncrementOnly));
            Assert.IsTrue(flags.HasFlag(StatFlags.Protected));
            Assert.IsFalse(flags.HasFlag(StatFlags.UnknownPermission));
        }
    }
}
