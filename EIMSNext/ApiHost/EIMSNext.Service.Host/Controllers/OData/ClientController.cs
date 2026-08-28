using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using EIMSNext.Common;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.OpenPlatform;
using EIMSNext.Service.Host.OData;
using EIMSNext.Service.Host.Requests;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// OAuth 客户端的 OData CRUD 控制器。
    ///
    /// 实体集：<c>Client</c>。仅 <c>IdentityTypeDefaults.CorpAdmin</c> 身份可访问。
    /// <c>ClientSecrets</c> 在 EDM 中被 <c>Ignore()</c>，永远不出现在 OData 响应/请求中；
    /// 改密需走 <c>ClientController.GenerateSecret</c> 端点。
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.CorpAdmin)]
    public class ClientController(IResolver resolver)
        : ODataController<ClientApiService, Client, ClientViewModel, ClientRequest>(resolver)
    {
        private ClientApiService ClientApi => Resolver.Resolve<ClientApiService>();
        private ClientGrantApiService ClientGrantApi => Resolver.Resolve<ClientGrantApiService>();

        [Permission(AccessControlLevel = AccessControlLevel.Forbid)]
        public override Task<ActionResult> Put([FromODataUri] string key, [FromBody] ClientRequest model)
        {
            return base.Put(key, model);
        }

        public override async Task<ActionResult> Patch([FromODataUri] string key, [FromBody] Delta<ClientRequest> delta)
        {
            var result = await base.Patch(key, delta);
            await RefreshClientCacheAsync(key);
            return result;
        }

        public override async Task<ActionResult> Patch([FromBody] DeltaSet<ClientRequest> deltas)
        {
            if (deltas == null)
            {
                return BadRequest("数据解析失败，请检查数据格式, 确认正确的字段名和数据类型");
            }

            var keys = deltas
                .OfType<Delta<ClientRequest>>()
                .Select(delta => TryGetId(delta, out var id) ? id : string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            var result = await base.Patch(deltas);
            foreach (var key in keys)
            {
                await RefreshClientCacheAsync(key);
            }
            return result;
        }

        public override async Task<ActionResult> Delete([FromODataUri] string key, [FromBody] DeleteBatch? batch)
        {
            var keys = "batch".EqualsIgnoreCase(key)
                ? batch?.Keys?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList() ?? []
                : [key];

            var result = await base.Delete(key, batch);
            foreach (var clientId in keys)
            {
                ClientPermissionCache.Evict(CacheClient, clientId);
            }
            return result;
        }

        private async Task RefreshClientCacheAsync(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return;
            }

            await ClientPermissionCache.RefreshAsync(CacheClient, ClientGrantApi, ClientApi, IdentityContext.CurrentCorpId, clientId);
        }
    }
}
