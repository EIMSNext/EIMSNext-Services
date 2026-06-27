using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using MongoDB.Driver;
using NanoidDotNet;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// OAuth 客户端的 API 服务。
    ///
    /// 安全要点：
    /// <list type="bullet">
    /// <item>明文 ClientSecret 只在 Create / GenerateSecret 时返回，存 DB 前 SHA-256 哈希。</item>
    /// <item>明文凭证额外缓存到 <c>IScopeCache</c> 5 分钟，<c>Reveal</c> 端点用它取回。</item>
    /// <item><c>UpdateAsync</c> 用 read-modify-write 保护 ClientSecrets/ClientId/ApiKey 永不被请求体改写。</item>
    /// </list>
    /// </summary>
    public class ClientApiService(IResolver resolver)
        : ApiServiceBase<Client, ClientViewModel, IClientService>(resolver), IClientApiService
    {
        private const string PlainCacheKeyPrefix = "clientSecret:plain:";

        // 32 字符 a-z + 数字 + 几个安全符号，作为 ClientId/Secret 的字符表
        private const string SecretAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        private const string ApiKeyAlphabet =
            "_+-0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()~`.,?=";

        // ===== 写操作 =====
        public async Task<ClientCredentials> CreateAsync(ClientRequest input)
        {
            var clientId = GenerateClientId();
            var plainSecret = GeneratePlainSecret();
            var apiKey = Nanoid.Generate(ApiKeyAlphabet, 36);

            var entity = new Client
            {
                ClientId = clientId,
                ClientSecrets = new List<ClientSecret>
                {
                    new() { Value = plainSecret.Sha256(), Type = "SharedSecret" }
                },
                ApiKey = apiKey,
                Enabled = input.Enabled,
                RequireClientSecret = input.RequireClientSecret,
                ClientName = input.ClientName,
                AllowedGrantTypes = input.AllowedGrantTypes,
                AllowedScopes = input.AllowedScopes,
                IdentityTokenLifetime = input.IdentityTokenLifetime,
                AccessTokenLifetime = input.AccessTokenLifetime,
                CorpId = IdentityContext.CurrentCorpId,
            };

            await AddAsync(entity);

            // 缓存明文 5 分钟（供 Reveal 端点使用）
            CachePlainSecret(entity.Id, plainSecret);

            return new ClientCredentials
            {
                ClientId = clientId,
                ClientSecret = plainSecret,
                ApiKey = apiKey,
            };
        }

        public async Task<Client> UpdateAsync(string id, ClientRequest input)
        {
            // read-modify-write：忽略输入中的 ClientSecrets/ClientId/ApiKey
            var existing = await CoreService.GetAsync(id);
            if (existing == null)
            {
                throw new BadRequestException("Client 不存在");
            }
            if (existing.CorpId != IdentityContext.CurrentCorpId)
            {
                throw new BadRequestException("无权访问该 Client");
            }

            // 只更新可写字段；ClientId/ApiKey/ClientSecrets/CorpId 永不动
            existing.Enabled = input.Enabled;
            existing.RequireClientSecret = input.RequireClientSecret;
            existing.ClientName = input.ClientName;
            existing.AllowedGrantTypes = input.AllowedGrantTypes;
            existing.AllowedScopes = input.AllowedScopes;
            existing.IdentityTokenLifetime = input.IdentityTokenLifetime;
            existing.AccessTokenLifetime = input.AccessTokenLifetime;

            await CoreService.ReplaceAsync(existing);
            return existing;
        }

        /// <summary>
        /// OData PATCH 直接走来的更新：保留 ClientSecrets/ClientId/ApiKey 不变。
        /// （用于前端 el-switch 启停等场景；input 中可只包含要改的字段。）
        /// </summary>
        public async Task PatchAsync(string id, Client patch)
        {
            var existing = await CoreService.GetAsync(id);
            if (existing == null)
            {
                throw new BadRequestException("Client 不存在");
            }
            if (existing.CorpId != IdentityContext.CurrentCorpId)
            {
                throw new BadRequestException("无权访问该 Client");
            }

            // 仅可改可写字段；不可改的字段保持 existing 原值
            if (patch.Enabled != existing.Enabled) existing.Enabled = patch.Enabled;
            if (patch.RequireClientSecret != existing.RequireClientSecret) existing.RequireClientSecret = patch.RequireClientSecret;
            if (patch.ClientName != null) existing.ClientName = patch.ClientName;
            if (patch.AllowedGrantTypes != null && patch.AllowedGrantTypes.Count > 0) existing.AllowedGrantTypes = patch.AllowedGrantTypes;
            if (patch.AllowedScopes != null && patch.AllowedScopes.Count > 0) existing.AllowedScopes = patch.AllowedScopes;
            if (patch.IdentityTokenLifetime > 0) existing.IdentityTokenLifetime = patch.IdentityTokenLifetime;
            if (patch.AccessTokenLifetime > 0) existing.AccessTokenLifetime = patch.AccessTokenLifetime;

            // ClientSecrets/ClientId/ApiKey/CorpId 永不被覆盖
            await CoreService.ReplaceAsync(existing);
        }

        public async Task<ClientCredentials> GenerateSecretAsync(string id)
        {
            var existing = await CoreService.GetAsync(id);
            if (existing == null)
            {
                throw new BadRequestException("Client 不存在");
            }
            if (existing.CorpId != IdentityContext.CurrentCorpId)
            {
                throw new BadRequestException("无权访问该 Client");
            }

            var plainSecret = GeneratePlainSecret();
            existing.ClientSecrets = new List<ClientSecret>
            {
                new() { Value = plainSecret.Sha256(), Type = "SharedSecret" }
            };

            await CoreService.ReplaceAsync(existing);

            CachePlainSecret(id, plainSecret);

            return new ClientCredentials
            {
                ClientId = existing.ClientId,
                ClientSecret = plainSecret,
                ApiKey = existing.ApiKey,
            };
        }

        public async Task<ClientCredentials> GenerateApiKeyAsync(string id)
        {
            var existing = await CoreService.GetAsync(id);
            if (existing == null)
            {
                throw new BadRequestException("Client 不存在");
            }
            if (existing.CorpId != IdentityContext.CurrentCorpId)
            {
                throw new BadRequestException("无权访问该 Client");
            }

            existing.ApiKey = Nanoid.Generate(ApiKeyAlphabet, 36);
            await CoreService.ReplaceAsync(existing);

            return new ClientCredentials
            {
                ClientId = existing.ClientId,
                ClientSecret = "",       // 不变
                ApiKey = existing.ApiKey,
            };
        }

        // ===== 读操作 =====
        public async Task<ClientCredentials> RevealAsync(string id)
        {
            var existing = await CoreService.GetAsync(id);
            if (existing == null)
            {
                throw new BadRequestException("Client 不存在");
            }
            if (existing.CorpId != IdentityContext.CurrentCorpId)
            {
                throw new BadRequestException("无权访问该 Client");
            }

            var plainSecret = TryGetPlainSecret(id);
            return new ClientCredentials
            {
                ClientId = existing.ClientId,
                ClientSecret = plainSecret ?? string.Empty,
                ApiKey = existing.ApiKey,
            };
        }

        public async Task<List<Client>> ListByCorpAsync()
        {
            var all = await CoreService.FindAsync(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag);
            return await all.ToListAsync();
        }

        // ===== helpers =====
        private static string GenerateClientId() => Nanoid.Generate(SecretAlphabet, 16);
        private static string GeneratePlainSecret() => Nanoid.Generate(SecretAlphabet, 40);

        private void CachePlainSecret(string clientId, string plain)
        {
            CacheClient.Set(
                PlainCacheKeyPrefix + clientId,
                plain,
                CacheScope.Client,
                clientId,
                new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });
        }

        private string? TryGetPlainSecret(string clientId)
        {
            return CacheClient.GetString(PlainCacheKeyPrefix + clientId, CacheScope.Client, clientId);
        }
    }
}
