using EIMSNext.ApiCore.RateLimiting;
using EIMSNext.Cache;
using Microsoft.Extensions.Caching.Memory;

namespace EIMSNext.Auth.Tests;

[TestClass]
public class VerificationCodeRateLimiterTests
{
    [TestMethod]
    public async Task SamePurposeAndTargetIsSubjectToCooldown()
    {
        var limiter = CreateLimiter();

        var first = await limiter.CheckAsync("register", " TESTER@EXAMPLE.COM ", "127.0.0.1");
        var second = await limiter.CheckAsync("register", "tester@example.com", "127.0.0.2");

        Assert.IsTrue(first.Allowed);
        Assert.IsFalse(second.Allowed);
        Assert.AreEqual(2, second.TargetCount);
    }

    [TestMethod]
    public async Task DifferentPurposesDoNotShareTargetLimit()
    {
        var limiter = CreateLimiter();

        var register = await limiter.CheckAsync("register", "13800138000", "127.0.0.1");
        var login = await limiter.CheckAsync("login", "13800138000", "127.0.0.1");

        Assert.IsTrue(register.Allowed);
        Assert.IsTrue(login.Allowed);
    }

    [TestMethod]
    public async Task TargetWindowStopsSixthRequestAcrossIps()
    {
        var limiter = CreateLimiter();
        VerificationCodeRateLimitResult last = default;

        for (var index = 0; index < VerificationCodeRateLimiter.TargetLimit + 1; index++)
        {
            last = await limiter.CheckAsync("bind", "13800138001", $"127.0.0.{index + 1}");
        }

        Assert.IsFalse(last.Allowed);
        Assert.AreEqual(VerificationCodeRateLimiter.TargetLimit + 1, last.TargetCount);
    }

    [TestMethod]
    public async Task IpWindowStopsTwentyFirstTarget()
    {
        var limiter = CreateLimiter();
        VerificationCodeRateLimitResult last = default;

        for (var index = 0; index < VerificationCodeRateLimiter.IpLimit + 1; index++)
        {
            last = await limiter.CheckAsync("login", $"user-{index}@example.com", "127.0.0.9");
        }

        Assert.IsFalse(last.Allowed);
        Assert.AreEqual(VerificationCodeRateLimiter.IpLimit + 1, last.IpCount);
    }

    private static VerificationCodeRateLimiter CreateLimiter()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        return new VerificationCodeRateLimiter(new FakeCacheClient(memoryCache));
    }
}
