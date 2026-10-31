using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EIMSNext.Identity.Interfaces;

namespace EIMSNext.Identity.AccountSecurity;

public static class VerificationCodePurpose
{
    public const string Register = "register";
    public const string Login = "login";
    public const string VerifyIdentity = "verify";
    public const string Bind = "bind";
    public const string ResetPassword = "resetPassword";
}

public sealed record VerificationCodeSendResult(DateTimeOffset ExpiresAt, string? MockCode);

public sealed class MockVerificationCodeProvider : IVerificationCodeProvider
{
    // 验证码校验侧没有端点限流，不限制错误尝试次数的话，5 分钟有效窗口内的 6 位码
    // 可被穷举（10^6 空间）。每个码最多允许 5 次错误尝试，超限即作废，须重新发送。
    public const int MaxAttempts = 5;

    private readonly object _gate = new();
    private readonly Dictionary<string, StoredCode> _codes = new(StringComparer.Ordinal);
    private readonly TimeSpan _lifetime;
    private readonly Func<DateTimeOffset> _clock;

    public MockVerificationCodeProvider(TimeSpan? lifetime = null, Func<DateTimeOffset>? clock = null)
    {
        _lifetime = lifetime ?? TimeSpan.FromMinutes(5);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public VerificationCodeSendResult Send(string purpose, string target)
    {
        var key = BuildKey(purpose, target);
        var now = _clock();
        var expiresAt = now.Add(_lifetime);

        lock (_gate)
        {
            RemoveExpired(now);

            string code;
            do
            {
                code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6", CultureInfo.InvariantCulture);
            }
            while (_codes.Values.Any(value => value.Code == code));

            _codes[key] = new StoredCode(code, expiresAt);
            return new VerificationCodeSendResult(expiresAt, code);
        }
    }

    public bool TryConsume(string purpose, string target, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var key = BuildKey(purpose, target);
        var now = _clock();

        lock (_gate)
        {
            if (!_codes.TryGetValue(key, out var storedCode))
            {
                return false;
            }

            if (storedCode.ExpiresAt <= now)
            {
                _codes.Remove(key);
                return false;
            }

            var expected = Encoding.UTF8.GetBytes(storedCode.Code);
            var actual = Encoding.UTF8.GetBytes(code.Trim());
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                // 错误尝试计满即作废当前码；重新 Send 会覆盖 StoredCode 并重置计数。
                var failed = storedCode with { FailCount = storedCode.FailCount + 1 };
                if (failed.FailCount >= MaxAttempts)
                {
                    _codes.Remove(key);
                }
                else
                {
                    _codes[key] = failed;
                }

                return false;
            }

            _codes.Remove(key);
            return true;
        }
    }

    public static string BuildKey(string purpose, string target)
    {
        var normalizedPurpose = purpose.Trim().ToLowerInvariant();
        var normalizedTarget = target.Trim();
        if (normalizedTarget.Contains('@', StringComparison.Ordinal))
        {
            normalizedTarget = normalizedTarget.ToLowerInvariant();
        }

        return $"{normalizedPurpose}:{normalizedTarget}";
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var pair in _codes.Where(pair => pair.Value.ExpiresAt <= now).ToArray())
        {
            _codes.Remove(pair.Key);
        }
    }

    private sealed record StoredCode(string Code, DateTimeOffset ExpiresAt, int FailCount = 0);
}
