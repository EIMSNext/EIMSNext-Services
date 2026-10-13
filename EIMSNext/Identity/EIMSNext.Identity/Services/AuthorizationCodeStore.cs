using System.Security.Cryptography;
using System.Text.Json;

using EIMSNext.Identity.Interfaces;

using Microsoft.Extensions.Caching.Distributed;

namespace EIMSNext.Identity.Services
{
    /// <summary>
    /// 基于分布式缓存的一次性授权码存储（多实例部署共享，默认 Redis）。
    /// </summary>
    public sealed class AuthorizationCodeStore : IAuthorizationCodeStore
    {
        private const string KeyPrefix = "identity:auth_code:";

        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly IDistributedCache _cache;

        public AuthorizationCodeStore(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<string> IssueAsync(string userId, string username, string? clientId, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            var code = RandomNumberGenerator.GetHexString(32, lowercase: true);
            var payload = JsonSerializer.Serialize(new AuthorizationCodePayload(userId, username, clientId), SerializerOptions);
            await _cache.SetStringAsync(
                Key(code),
                payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken);
            return code;
        }

        public async Task<AuthorizationCodePayload?> TryConsumeAsync(string code, string? clientId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var key = Key(code);
            var payload = await _cache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            // 无论后续校验成败都立即作废：授权码只允许被消费一次
            await _cache.RemoveAsync(key, cancellationToken);

            AuthorizationCodePayload? issued;
            try
            {
                issued = JsonSerializer.Deserialize<AuthorizationCodePayload>(payload, SerializerOptions);
            }
            catch (JsonException)
            {
                return null;
            }

            if (issued == null)
            {
                return null;
            }

            if (!string.Equals(issued.ClientId ?? string.Empty, clientId ?? string.Empty, StringComparison.Ordinal))
            {
                return null;
            }

            return issued;
        }

        private static string Key(string code) => KeyPrefix + code;
    }
}
