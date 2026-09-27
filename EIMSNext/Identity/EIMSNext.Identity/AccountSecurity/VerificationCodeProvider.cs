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
}

public sealed record VerificationCodeSendResult(DateTimeOffset ExpiresAt, string? MockCode);

public sealed class MockVerificationCodeProvider : IVerificationCodeProvider
{
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

    private sealed record StoredCode(string Code, DateTimeOffset ExpiresAt);
}
