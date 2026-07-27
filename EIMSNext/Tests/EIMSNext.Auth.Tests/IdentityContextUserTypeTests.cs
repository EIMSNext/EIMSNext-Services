using EIMSNext.ApiHost.Authorization;
using EIMSNext.ApiService;
using EIMSNext.Auth.Entities;

namespace EIMSNext.Auth.Tests
{
    [TestClass]
    public class IdentityContextUserTypeTests
    {
        [TestMethod]
        public void ResolveExplicitIdentityType_UsesExplicitValueAndDisabledWins()
        {
            var user = new User { UserType = "platadmin" };
            Assert.AreEqual(IdentityType.PlatAdmin, IdentityContext.ResolveExplicitIdentityType(user));

            user.Disabled = true;
            Assert.AreEqual(IdentityType.Disabled, IdentityContext.ResolveExplicitIdentityType(user));
        }

        [TestMethod]
        public void ResolveExplicitIdentityType_LeavesLegacyCalculationForEmptyValue()
        {
            Assert.IsNull(IdentityContext.ResolveExplicitIdentityType(new User()));
            Assert.AreEqual(
                IdentityType.None,
                IdentityContext.ResolveExplicitIdentityType(new User { UserType = "not-a-valid-identity" }));
        }
    }
}
