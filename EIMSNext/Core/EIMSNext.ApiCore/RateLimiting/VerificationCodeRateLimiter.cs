using System.Security.Cryptography;
using System.Text;
using EIMSNext.Cache;

namespace EIMSNext.ApiCore.RateLimiting;

public sealed class VerificationCodeRateLimiter
{
    public const int TargetLimit = 5;
    public static readonly TimeSpan TargetWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan TargetCooldown = TimeSpan.FromMinutes(1);
    public const int IpLimit = 20;
    public static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(10);

    private readonly ICacheClient _cache;

    public VerificationCodeRateLimiter(ICacheClient cache)
    {
        _cache = cache;
    }

    public async Task<VerificationCodeRateLimitResult> CheckAsync(
        string purpose,
        string target,
        string ip,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPurpose = Normalize(purpose);
        var normalizedTarget = Normalize(target);
        var normalizedIp = Normalize(ip);
        if (string.IsNullOrWhiteSpace(normalizedPurpose) ||
            string.IsNullOrWhiteSpace(normalizedTarget) ||
            string.IsNullOrWhiteSpace(normalizedIp))
        {
            return VerificationCodeRateLimitResult.AllowedResult;
        }

        var targetHash = Hash(normalizedTarget);
        var cooldown = await _cache.IncrementAsync(
            $"rl:verification-code:cooldown:{normalizedPurpose}:{targetHash}",
            1,
            TargetCooldown,
            CacheScope.Global);
        var targetCount = await _cache.IncrementAsync(
            $"rl:verification-code:target:{normalizedPurpose}:{targetHash}",
            1,
            TargetWindow,
            CacheScope.Global);
        var ipCount = await _cache.IncrementAsync(
            $"rl:verification-code:ip:{Hash(normalizedIp)}",
            1,
            IpWindow,
            CacheScope.Global);

        return new VerificationCodeRateLimitResult(
            cooldown <= 1 && targetCount <= TargetLimit && ipCount <= IpLimit,
            cooldown,
            targetCount,
            ipCount);
    }

    public static string Normalize(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Contains('@', StringComparison.Ordinal)
            ? normalized.ToLowerInvariant()
            : normalized;
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}

public readonly record struct VerificationCodeRateLimitResult(
    bool Allowed,
    long CooldownCount,
    long TargetCount,
    long IpCount)
{
    public static VerificationCodeRateLimitResult AllowedResult => new(true, 0, 0, 0);
}
