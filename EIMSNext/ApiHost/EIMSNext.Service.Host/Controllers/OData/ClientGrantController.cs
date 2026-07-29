using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.OpenPlatform;
using EIMSNext.Service.Host.Requests;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 客户端授权的 OData CRUD 控制器。
    /// 实体集：<c>ClientGrant</c>，仅 CorpAdmin 可访问（由 <c>IdentityTypeFilter</c> 默认设置保证）。
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class ClientGrantController(IResolver resolver)
        : ODataController<ClientGrantApiService, ClientGrant, ClientGrantViewModel, ClientGrantRequest>(resolver)
    {
        private ClientApiService ClientApi => Resolver.Resolve<ClientApiService>();
        private ClientGrantApiService ClientGrantApi => Resolver.Resolve<ClientGrantApiService>();

        public override async Task<ActionResult> Post([FromBody] ClientGrantRequest model)
        {
            if (model == null)
            {
                return BadRequest("请求体不能为空");
            }

            var validation = ValidateIpWhitelist(model.IpWhitelist);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            var result = await base.Post(model);
            await RefreshClientCacheAsync(model.ClientId);
            return result;
        }

        public override async Task<ActionResult> Put([FromODataUri] string key, [FromBody] ClientGrantRequest model)
        {
            if (model == null)
            {
                return BadRequest("请求体不能为空");
            }

            var validation = ValidateIpWhitelist(model.IpWhitelist);
            if (validation != null)
            {
                return BadRequest(validation);
            }

            var previousClientId = await GetClientIdAsync(key);
            var result = await base.Put(key, model);
            await RefreshClientCacheAsync(previousClientId);
            await RefreshClientCacheAsync(model.ClientId);
            return result;
        }

        public override async Task<ActionResult> Patch([FromODataUri] string key, [FromBody] Delta<ClientGrantRequest> delta)
        {
            if (TryGetIpWhitelist(delta, out var ipWhitelist))
            {
                var validation = ValidateIpWhitelist(ipWhitelist);
                if (validation != null)
                {
                    return BadRequest(validation);
                }
            }

            var previousClientId = await GetClientIdAsync(key);
            var result = await base.Patch(key, delta);
            var currentClientId = await GetClientIdAsync(key);
            await RefreshClientCacheAsync(previousClientId);
            await RefreshClientCacheAsync(currentClientId);
            return result;
        }

        public override async Task<ActionResult> Patch([FromBody] DeltaSet<ClientGrantRequest> deltas)
        {
            if (deltas == null)
            {
                return BadRequest("数据解析失败，请检查数据格式, 确认正确的字段名和数据类型");
            }

            var keys = deltas
                .OfType<Delta<ClientGrantRequest>>()
                .Select(delta => TryGetId(delta, out var id) ? id : string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
            var previousClientIds = await GetClientIdsAsync(keys);

            var result = await base.Patch(deltas);
            var currentClientIds = await GetClientIdsAsync(keys);
            foreach (var clientId in previousClientIds.Concat(currentClientIds).Distinct())
            {
                await RefreshClientCacheAsync(clientId);
            }
            return result;
        }

        public override async Task<ActionResult> Delete([FromODataUri] string key, [FromBody] DeleteBatch? batch)
        {
            var keys = "batch".EqualsIgnoreCase(key)
                ? batch?.Keys?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList() ?? []
                : [key];
            var clientIds = await GetClientIdsAsync(keys);

            var result = await base.Delete(key, batch);
            foreach (var clientId in clientIds)
            {
                ClientPermissionCache.Evict(CacheClient, clientId);
            }
            return result;
        }

        private async Task<string> GetClientIdAsync(string grantId)
        {
            var grant = string.IsNullOrWhiteSpace(grantId) ? null : await ApiService.GetAsync(grantId);
            return grant?.ClientId ?? string.Empty;
        }

        private async Task<List<string>> GetClientIdsAsync(IEnumerable<string> grantIds)
        {
            var clientIds = new List<string>();
            foreach (var grantId in grantIds)
            {
                var clientId = await GetClientIdAsync(grantId);
                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    clientIds.Add(clientId);
                }
            }
            return clientIds;
        }

        private async Task RefreshClientCacheAsync(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return;
            }

            await ClientPermissionCache.RefreshAsync(CacheClient, ClientGrantApi, ClientApi, IdentityContext.CurrentCorpId, clientId);
        }

        private static string? ValidateIpWhitelist(IEnumerable<string>? rules)
        {
            if (rules == null)
            {
                return null;
            }

            foreach (var raw in rules)
            {
                var rule = raw?.Trim() ?? string.Empty;
                if (IpMatcher.IsValidRule(rule))
                {
                    continue;
                }

                return $"IP 白名单包含无效地址：{raw}";
            }

            return null;
        }

        private static bool TryGetIpWhitelist(Delta<ClientGrantRequest> delta, out IEnumerable<string> rules)
        {
            rules = [];
            if (!delta.TryGetPropertyValue(nameof(ClientGrantRequest.IpWhitelist), out var value)
                && !delta.TryGetPropertyValue("ipWhitelist", out value))
            {
                return false;
            }

            if (value is IEnumerable<string> strings)
            {
                rules = strings;
                return true;
            }

            return false;
        }
    }
}
