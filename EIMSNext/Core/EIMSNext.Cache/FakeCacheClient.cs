using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace EIMSNext.Cache
{
    public sealed class FakeCacheClient : ICacheClient
    {
        private readonly IMemoryCache _cache;

        public FakeCacheClient(IMemoryCache cache)
        {
            _cache = cache;
        }

        public T? Get<T>(string key, CacheScope scope, string scopeId = "")
        {
            return _cache.Get<T>(GetKey(key, scope, scopeId));
        }

        public Task<T?> GetAsync<T>(string key, CacheScope scope, string scopeId = "")
        {
            return Task.FromResult(Get<T>(key, scope, scopeId));
        }

        public string? GetString(string key, CacheScope scope, string scopeId = "")
        {
            return _cache.Get<string>(GetKey(key, scope, scopeId));
        }

        public Task<string?> GetStringAsync(string key, CacheScope scope, string scopeId = "")
        {
            return Task.FromResult(GetString(key, scope, scopeId));
        }

        public void Refresh(string key, CacheScope scope, string scopeId = "")
        {
        }

        public Task RefreshAsync(string key, CacheScope scope, string scopeId = "")
        {
            return Task.CompletedTask;
        }

        public void Remove(string key, CacheScope scope, string scopeId = "")
        {
            _cache.Remove(GetKey(key, scope, scopeId));
        }

        public Task RemoveAsync(string key, CacheScope scope, string scopeId = "")
        {
            return Task.CompletedTask;
        }

        public void Set<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
            _cache.Set(GetKey(key, scope, scopeId), value, GetOptions(options));
        }

        public Task SetAsync<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
            Set(key, value, scope, scopeId, options);
            return Task.CompletedTask;
        }

        public void SetString(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
            _cache.Set(GetKey(key, scope, scopeId), value, GetOptions(options));
        }

        public Task SetStringAsync(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
            SetString(key, value, scope, scopeId, options);
            return Task.CompletedTask;
        }

        private static string GetKey(string key, CacheScope scope, string scopeId = "")
        {
            return string.IsNullOrEmpty(scopeId) ? $"{scope:G}".ToUpperInvariant() + $":{key}" : $"{scope:G}".ToUpperInvariant() + $":{scopeId}:{key}";
        }

        private static MemoryCacheEntryOptions GetOptions(DistributedCacheEntryOptions? options)
        {
            var memoryOptions = new MemoryCacheEntryOptions();

            if (options == null)
            {
                return memoryOptions;
            }

            memoryOptions.AbsoluteExpiration = options.AbsoluteExpiration;
            memoryOptions.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow;
            memoryOptions.SlidingExpiration = options.SlidingExpiration;
            return memoryOptions;
        }
    }
}
