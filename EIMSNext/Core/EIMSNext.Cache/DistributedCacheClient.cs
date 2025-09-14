using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace EIMSNext.Cache
{
    public class DistributedCacheClient : ICacheClient
    {
        public DistributedCacheClient(IDistributedCache cache)
        {
            _cache = cache;
        }

        private readonly IDistributedCache _cache;

        public void SetString(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
            var cacheKey = GetKey(key, scope, scopeId);
            if (options == null)
                _cache.SetString(cacheKey, value);
            else
                _cache.SetString(cacheKey, value, options);
        }
        public Task SetStringAsync(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
            var cacheKey = GetKey(key, scope, scopeId);
            if (options == null)
                return _cache.SetStringAsync(cacheKey, value);
            else
                return _cache.SetStringAsync(cacheKey, value, options);
        }

        public string? GetString(string key, CacheScope scope, string scopeId = "")
        {
            var cacheKey = GetKey(key, scope, scopeId);
            return _cache.GetString(cacheKey);
        }
        public Task<string?> GetStringAsync(string key, CacheScope scope, string scopeId = "")
        {
            var cacheKey = GetKey(key, scope, scopeId);
            return _cache.GetStringAsync(cacheKey);
        }

        public void Set<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
            var cacheKey = GetKey(key, scope, scopeId);
            if (options == null)
                _cache.Set(cacheKey, GetBytes(value));
            else
                _cache.Set(cacheKey, GetBytes(value), options);
        }
        public Task SetAsync<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null)
        {
            var cacheKey = GetKey(key, scope, scopeId);
            if (options == null)
                return _cache.SetAsync(cacheKey, GetBytes(value));
            else
                return _cache.SetAsync(cacheKey, GetBytes(value), options);
        }

        public T? Get<T>(string key, CacheScope scope, string scopeId = "")
        {
            var cacheKey = GetKey(key, scope, scopeId);
            return FromBytes<T>(_cache.Get(cacheKey));
        }
        public async Task<T?> GetAsync<T>(string key, CacheScope scope, string scopeId = "")
        {
            var cacheKey = GetKey(key, scope, scopeId);
            return FromBytes<T>(await _cache.GetAsync(cacheKey));
        }

        public void Refresh(string key, CacheScope scope, string scopeId = "")
        {
            var cacheKey = GetKey(key, scope, scopeId);
            _cache.Refresh(cacheKey);
        }
        public Task RefreshAsync(string key, CacheScope scope, string scopeId = "")
        {
            var cacheKey = GetKey(key, scope, scopeId);
            return _cache.RefreshAsync(cacheKey);
        }

        public void Remove(string key, CacheScope scope, string scopeId = "")
        {
            var cacheKey = GetKey(key, scope, scopeId);
            _cache.Remove(cacheKey);
        }
        public Task RemoveAsync(string key, CacheScope scope, string scopeId = "")
        {
            var cacheKey = GetKey(key, scope, scopeId);
            return _cache.RemoveAsync(cacheKey);
        }

        private string GetKey(string key, CacheScope scope, string scopeId = "")
        {
            return string.IsNullOrEmpty(scopeId) ? $"{scope.ToString("G").ToUpper()}:{key}" : $"{scope.ToString("G").ToUpper()}:{scopeId}:{key}";
        }
        private byte[] GetBytes<T>(T value)
        {
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
        }
        private T? FromBytes<T>(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return default(T);
            return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(bytes));
        }
    }
}
