using System.IdentityModel.Tokens.Jwt;
using EIMSNext.ApiCore;
using Microsoft.Extensions.Caching.Distributed;

namespace EIMSNext.Identity.Tests
{
    [TestClass]
    public class LogoutTokenStoreTests
    {
        [TestMethod]
        public async Task MarkLoggedOutAsync_UsesSha256CacheKeyAndExpiration()
        {
            var cache = new RecordingDistributedCache();
            var store = new DistributedLogoutTokenStore(cache);
            var token = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.signature";
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);

            await store.MarkLoggedOutAsync(token, expiresAt);

            Assert.AreEqual(LogoutTokenHelper.GetCacheKey(token), cache.LastKey);
            StringAssert.StartsWith(cache.LastKey!, "identity:logout:");
            Assert.IsFalse(cache.LastKey!.Contains(token, StringComparison.Ordinal));
            Assert.AreEqual("logout", cache.GetStoredString(cache.LastKey!));
            Assert.AreEqual(expiresAt.ToUnixTimeSeconds(), cache.LastOptions!.AbsoluteExpiration!.Value.ToUnixTimeSeconds());
        }

        [TestMethod]
        public async Task IsLoggedOutAsync_ReturnsTrueOnlyForMarkedToken()
        {
            var cache = new RecordingDistributedCache();
            var store = new DistributedLogoutTokenStore(cache);
            var token = "token-a";

            await store.MarkLoggedOutAsync(token, DateTimeOffset.UtcNow.AddMinutes(5));

            Assert.IsTrue(await store.IsLoggedOutAsync(token));
            Assert.IsFalse(await store.IsLoggedOutAsync("token-b"));
        }

        [TestMethod]
        public void ReadExpirationUtc_ReadsExpFromJwt()
        {
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
            var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(expires: expiresAt.UtcDateTime));

            var parsed = LogoutTokenHelper.ReadExpirationUtc(token);

            Assert.IsNotNull(parsed);
            Assert.AreEqual(expiresAt.ToUnixTimeSeconds(), parsed.Value.ToUnixTimeSeconds());
        }

        private sealed class RecordingDistributedCache : IDistributedCache
        {
            private readonly Dictionary<string, CacheItem> _cache = new(StringComparer.Ordinal);

            public string? LastKey { get; private set; }

            public DistributedCacheEntryOptions? LastOptions { get; private set; }

            public byte[]? Get(string key)
            {
                return _cache.TryGetValue(key, out var item) ? item.Value : null;
            }

            public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            {
                return Task.FromResult(Get(key));
            }

            public void Refresh(string key)
            {
            }

            public Task RefreshAsync(string key, CancellationToken token = default)
            {
                return Task.CompletedTask;
            }

            public void Remove(string key)
            {
                _cache.Remove(key);
            }

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                _cache.Remove(key);
                return Task.CompletedTask;
            }

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            {
                LastKey = key;
                LastOptions = options;
                _cache[key] = new CacheItem(value, options);
            }

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                Set(key, value, options);
                return Task.CompletedTask;
            }

            public string? GetStoredString(string key)
            {
                return _cache.TryGetValue(key, out var item) ? System.Text.Encoding.UTF8.GetString(item.Value) : null;
            }

            private sealed record CacheItem(byte[] Value, DistributedCacheEntryOptions Options);
        }
    }
}
