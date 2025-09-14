using Microsoft.Extensions.Caching.Distributed;

namespace EIMSNext.Cache
{
    public interface ICacheClient
    {
        void SetString(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null);
        Task SetStringAsync(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null);

        string? GetString(string key, CacheScope scope, string scopeId = "");
        Task<string?> GetStringAsync(string key, CacheScope scope, string scopeId = "");

        void Set<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null);
        Task SetAsync<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null);

        T? Get<T>(string key, CacheScope scope, string scopeId = "");
        Task<T?> GetAsync<T>(string key, CacheScope scope, string scopeId = "");

        void Refresh(string key, CacheScope scope, string scopeId = "");
        Task RefreshAsync(string key, CacheScope scope, string scopeId = "");

        void Remove(string key, CacheScope scope, string scopeId = "");
        Task RemoveAsync(string key, CacheScope scope, string scopeId = "");
    }
    public enum CacheScope
    {
        Corporate, Employee, Global
    }
}
