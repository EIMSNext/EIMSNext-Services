using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace EIMSNext.Identity.Services
{
    /// <summary>
    /// 公开访问的 HMAC 密钥存于 Client 表 public 行的 ClientSecret 列（seed 空库首建生成，轮换走 DB 手动 SQL）。
    /// 该密钥仅在身份宿主进程内使用（challenge 签发/校验），不参与 OAuth client 认证——
    /// public client 本身仍 RequireClientSecret=false。
    /// 进程内缓存 5 分钟：轮换后最迟 5 分钟生效；空值不缓存，密钥缺失仍即时 fail-closed。
    /// </summary>
    public sealed class PublicAccessKeyProvider
    {
        private const string CacheKey = "identity:public-access-secret-key";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        private readonly IIdentityDbContext _dbContext;
        private readonly IMemoryCache _cache;

        public PublicAccessKeyProvider(IIdentityDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        public string GetSecretKey()
        {
            if (_cache.TryGetValue(CacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            var secret = _dbContext.Clients
                .Where(x => x.Id == InternalClients.PublicClientId)
                .Select(x => x.ClientSecret)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(secret))
            {
                return string.Empty;
            }

            _cache.Set(CacheKey, secret, CacheTtl);
            return secret;
        }
    }
}
