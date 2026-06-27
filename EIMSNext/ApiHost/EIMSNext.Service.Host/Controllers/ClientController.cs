using Asp.Versioning;

using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Common;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.OpenPlatform;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 开放平台 Client 的自定义端点。
    ///
    /// OData 标准 CRUD 走 <c>Controllers/OData/ClientController</c>；
    /// 这里只放需要返回明文凭证的安全敏感端点。
    /// </summary>
    [ApiVersion(1.0)]
    [ApiController]
    [Authorize]
    [Route("api/v{version:apiVersion}/[controller]")]
    [IdentityType(IdentityTypeDefaults.CorpAdmin)]
    public class ClientController(IResolver resolver) : MefControllerBase(resolver)
    {
        private IClientApiService ClientApi => Resolver.Resolve<IClientApiService>();
        private IClientGrantApiService ClientGrantApi => Resolver.Resolve<IClientGrantApiService>();
        private ClientGrantApiService ClientGrantApiTyped => Resolver.Resolve<ClientGrantApiService>();

        /// <summary>创建 Client：生成 ClientId + ClientSecret + ApiKey，返回明文一次。</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClientRequest request)
        {
            var creds = await ClientApi.CreateAsync(request);
            // 预热权限码缓存（让后续 token 立即能命中）
            await WarmCacheForClientAsync(creds.ClientId);
            return ApiResult.Success(creds).ToActionResult();
        }

        /// <summary>
        /// 更新 Client：read-modify-write 保护 ClientSecrets/ClientId/ApiKey。
        /// （虽然 OData PATCH 也可走，但走这里会执行相同的 read-modify-write 保护。）
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] string id, [FromBody] ClientRequest request)
        {
            var entity = await ClientApi.UpdateAsync(id, request);
            // 刷新权限码缓存（如果 ClientName 改了不影响权限；如未来加 Enabled 字段则需要）
            await WarmCacheForClientAsync(entity.ClientId);
            // 返回不含 ClientSecrets 的视图（响应序列化时已 Ignore，但仍显式置空）
            entity.ClientSecrets = new();
            return ApiResult.Success(entity).ToActionResult();
        }

        /// <summary>查询当前可见的明文凭证。命中 5 分钟缓存才返回 clientSecret。</summary>
        [HttpGet("{id}/reveal")]
        public async Task<IActionResult> Reveal([FromRoute] string id)
        {
            var creds = await ClientApi.RevealAsync(id);
            return ApiResult.Success(creds).ToActionResult();
        }

        /// <summary>重新生成 ClientSecret。返回新明文 + 命中 5 分钟缓存。</summary>
        [HttpPost("{id}/generate-secret")]
        public async Task<IActionResult> GenerateSecret([FromRoute] string id)
        {
            var creds = await ClientApi.GenerateSecretAsync(id);
            await WarmCacheForClientAsync(creds.ClientId);
            return ApiResult.Success(creds).ToActionResult();
        }

        /// <summary>重新生成 ApiKey。返回新 ApiKey。</summary>
        [HttpPost("{id}/generate-api-key")]
        public async Task<IActionResult> GenerateApiKey([FromRoute] string id)
        {
            var creds = await ClientApi.GenerateApiKeyAsync(id);
            return ApiResult.Success(creds).ToActionResult();
        }

        private async Task WarmCacheForClientAsync(string clientId)
        {
            try
            {
                await ClientPermissionCache.RefreshAsync(Cache, ClientGrantApiTyped, IdentityContext.CurrentCorpId, clientId);
            }
            catch
            {
                // 缓存预热失败不影响主流程；下次请求时再重建
            }
        }
    }
}
