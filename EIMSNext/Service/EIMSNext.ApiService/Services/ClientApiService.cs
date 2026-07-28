using EIMSNext.ApiService.ViewModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core;
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
    /// <item><c>ReplaceAsyncCore</c> 用 read-modify-write 保护 ClientSecrets/Id/ApiKey 永不被请求体改写。</item>
    /// </list>
    /// </summary>
    public class ClientApiService(IResolver resolver)
        : ApiServiceBase<Client, ClientViewModel, IClientService>(resolver), IClientApiService
    {
        private const string PlainCacheKeyPrefix = "clientSecret:plain:";

        // 32 字符 a-z + 数字，作为 Secret 的字符表
        private const string SecretAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        private const string ApiKeyAlphabet =
            "_+-0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()~`.,?=";

        protected override async Task AddAsyncCore(Client entity)
        {
            var plainSecret = GeneratePlainSecret();

            entity.CorpId = IdentityContext.CurrentCorpId;
            entity.Id = string.Empty;
            Resolver.GetRepository<Client>().EnsureId(entity);
            entity.ClientSecrets =
            [
                new ClientSecret { Value = plainSecret.Sha256(), Type = "SharedSecret" }
            ];
            entity.ApiKey = Nanoid.Generate(ApiKeyAlphabet, 36);
            entity.RequireClientSecret = true;
            entity.AllowedGrantTypes =
            [
                new ClientGrantType { GrantType = "client_credentials" }
            ];
            entity.AllowedScopes =
            [
                new ClientScope { Scope = "api.readwrite" }
            ];
            entity.IdentityTokenLifetime = 7200;
            entity.AccessTokenLifetime = 7200;

            NormalizeEditableFields(entity);

            await base.AddAsyncCore(entity);

            CachePlainSecret(entity.Id, plainSecret);
        }

        protected override async Task<ReplaceOneResult> ReplaceAsyncCore(Client entity)
        {
            var existing = await CoreService.GetAsync(entity.Id);
            if (existing == null || existing.CorpId != IdentityContext.CurrentCorpId || existing.DeleteFlag)
            {
                throw new BadRequestException("Client 不存在");
            }

            entity.CorpId = existing.CorpId;
            entity.Id = existing.Id;
            entity.ClientSecrets = existing.ClientSecrets;
            entity.ApiKey = existing.ApiKey;
            entity.RequireClientSecret = existing.RequireClientSecret;
            entity.AllowedGrantTypes = existing.AllowedGrantTypes;
            entity.AllowedScopes = existing.AllowedScopes;
            entity.IdentityTokenLifetime = existing.IdentityTokenLifetime;
            entity.AccessTokenLifetime = existing.AccessTokenLifetime;
            entity.CreateBy = existing.CreateBy;
            entity.CreateTime = existing.CreateTime;

            NormalizeEditableFields(entity);

            return await base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
            if (idList.Count == 0)
            {
                return new object();
            }

            var deleting = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .Select(x => x.Id)
                .ToList();

            if (deleting.Count != idList.Count)
            {
                throw new BadRequestException("Client 不存在");
            }

            return await base.DeleteAsyncCore(deleting);
        }

        // ===== 写操作 =====
        public async Task<ClientCredentials> GenerateSecretAsync(string id)
        {
            var existing = await CoreService.GetAsync(id);
            if (existing == null || existing.DeleteFlag)
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
                ClientId = existing.Id,
                ClientSecret = plainSecret
            };
        }

        // ===== 读操作 =====
        public async Task<ClientCredentials> RevealAsync(string id)
        {
            var existing = await CoreService.GetAsync(id);
            if (existing == null || existing.DeleteFlag)
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
                ClientId = existing.Id,
                ClientSecret = plainSecret ?? string.Empty
            };
        }

        private static void NormalizeEditableFields(Client entity)
        {
            entity.Name = string.IsNullOrWhiteSpace(entity.Name)
                ? null
                : entity.Name.Trim();
        }

        // ===== helpers =====
        private static string GeneratePlainSecret() => Nanoid.Generate(SecretAlphabet, 40);

        private void CachePlainSecret(string clientId, string plain)
        {
            CacheClient.SetString(
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
