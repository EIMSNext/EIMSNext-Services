using Microsoft.Extensions.Caching.Distributed;

namespace EIMSNext.Cache
{
    public sealed class FakeCacheClient : ICacheClient
    {
        public T? Get<T>(string key, CacheScope scope, string scopeId = "")
        {
            return default(T);
        }

        public Task<T?> GetAsync<T>(string key, CacheScope scope, string scopeId = "")
        {
            return Task.FromResult(default(T));
        }

        public string? GetString(string key, CacheScope scope, string scopeId = "")
        {
            return null;
        }

        public Task<string?> GetStringAsync(string key, CacheScope scope, string scopeId = "")
        {
            return Task.FromResult<string?>(null);
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
        }

        public Task RemoveAsync(string key, CacheScope scope, string scopeId = "")
        {
            return Task.CompletedTask;
        }

        public void Set<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
        }

        public Task SetAsync<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
            return Task.CompletedTask;
        }

        public void SetString(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
        }

        public Task SetStringAsync(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
            return Task.CompletedTask;
        }
    }
}
