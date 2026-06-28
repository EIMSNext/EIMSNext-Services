using Asp.Versioning;

using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
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
        private ClientApiService ClientApiTyped => Resolver.Resolve<ClientApiService>();
        private ClientGrantApiService ClientGrantApiTyped => Resolver.Resolve<ClientGrantApiService>();

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

        private async Task WarmCacheForClientAsync(string clientId)
        {
            try
            {
                await ClientPermissionCache.RefreshAsync(Cache, ClientGrantApiTyped, ClientApiTyped, IdentityContext.CurrentCorpId, clientId);
            }
            catch
            {
                // 缓存预热失败不影响主流程；下次请求时再重建
            }
        }
    }
}
