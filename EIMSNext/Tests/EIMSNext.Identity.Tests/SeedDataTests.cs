using EIMSNext.Identity.Host;

namespace EIMSNext.Identity.Tests
{
    [TestClass]
    public class SeedDataTests
    {
        [TestMethod]
        public void GetUsers_SeedsPlatAdminWithCnEmailDomain()
        {
            var user = SeedData.GetUsers().Single(x => x.Id == "cloudadmin");

            Assert.AreEqual("cloudadmin@easyun.cn", user.Email);
            Assert.AreEqual("platadmin", user.UserType);
        }
    }
}
