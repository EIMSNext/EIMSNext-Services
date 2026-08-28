using EIMSNext.Identity.AccountSecurity;

namespace EIMSNext.Identity.Tests;

[TestClass]
public class VerificationCodeProviderTests
{
    [TestMethod]
    public void CodeIsScopedByPurposeAndTarget()
    {
        var provider = new MockVerificationCodeProvider();
        var register = provider.Send(VerificationCodePurpose.Register, "13800138000");
        var otherTarget = provider.Send(VerificationCodePurpose.Register, "13800138001");
        var otherPurpose = provider.Send(VerificationCodePurpose.Login, "13800138000");

        Assert.AreNotEqual(register.MockCode, otherTarget.MockCode);
        Assert.AreNotEqual(register.MockCode, otherPurpose.MockCode);
        Assert.IsFalse(provider.TryConsume(VerificationCodePurpose.Register, "13800138001", register.MockCode));
        Assert.IsFalse(provider.TryConsume(VerificationCodePurpose.Login, "13800138000", register.MockCode));
        Assert.IsTrue(provider.TryConsume(VerificationCodePurpose.Register, "13800138000", register.MockCode));
    }

    [TestMethod]
    public void CodeIsOneTimeAndExpires()
    {
        var now = DateTimeOffset.UtcNow;
        var provider = new MockVerificationCodeProvider(TimeSpan.FromMinutes(5), () => now);
        var result = provider.Send(VerificationCodePurpose.Register, "tester@example.com");

        Assert.IsTrue(provider.TryConsume(VerificationCodePurpose.Register, " TESTER@EXAMPLE.COM ", result.MockCode));
        Assert.IsFalse(provider.TryConsume(VerificationCodePurpose.Register, "tester@example.com", result.MockCode));

        var expired = provider.Send(VerificationCodePurpose.Register, "expired@example.com");
        now = expired.ExpiresAt.AddSeconds(1);
        Assert.IsFalse(provider.TryConsume(VerificationCodePurpose.Register, "expired@example.com", expired.MockCode));
    }

    [TestMethod]
    public void BuildKeyUsesPurposeAndNormalizedTarget()
    {
        Assert.AreEqual(
            "register:tester@example.com",
            MockVerificationCodeProvider.BuildKey(" REGISTER ", " TESTER@EXAMPLE.COM "));
        Assert.AreEqual(
            "bind:13800138000",
            MockVerificationCodeProvider.BuildKey("bind", " 13800138000 "));
    }
}
