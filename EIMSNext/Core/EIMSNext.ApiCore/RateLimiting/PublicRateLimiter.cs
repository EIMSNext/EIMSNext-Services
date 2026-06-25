using EIMSNext.Cache;

namespace EIMSNext.ApiCore.RateLimiting
{
    public sealed class PublicRateLimiter
    {
        private readonly ICacheClient _cache;

        public const int DefaultLimit = 5;
        public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1);

        public PublicRateLimiter(ICacheClient cache)
        {
            _cache = cache;
        }

        public async Task<PublicRateLimitResult> CheckAsync(string action, string targetId, string ip, int limit = DefaultLimit, TimeSpan? window = null)
        {
            if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(ip))
            {
                return new PublicRateLimitResult(true, 0, limit, window ?? DefaultWindow);
            }

            var key = $"rl:public_{action}:{ip}:{targetId}";
            var ttl = window ?? DefaultWindow;
            var count = await _cache.IncrementAsync(key, 1, ttl, CacheScope.Global);
            return new PublicRateLimitResult(count <= limit, count, limit, ttl);
        }
    }

    public readonly record struct PublicRateLimitResult(bool IsAllowed, long Count, int Limit, TimeSpan Window)
    {
        public bool Allowed => IsAllowed;
    }
}
