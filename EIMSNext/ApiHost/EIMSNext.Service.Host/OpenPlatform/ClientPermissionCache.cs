using EIMSNext.ApiService;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Authorization;

using Microsoft.Extensions.Caching.Distributed;

namespace EIMSNext.Service.Host.OpenPlatform
{
    /// <summary>
    /// Client 资源-动作权限 + 接入控制信息（IP 白名单 / Enabled）的缓存投影。
    ///
    /// <para>
    /// 把 <see cref="ClientGrant"/> 投影成 <see cref="ClientPermissionInfo"/> 写入 <c>ICacheClient</c>，
    /// 供 <c>PermissionFilter</c> 在 <c>IdentityType.Client</c> 路径下做：
    /// </para>
    /// <list type="number">
    /// <item>Enabled 检查（被禁用直接 403）</item>
    /// <item>IP 白名单检查（不在白名单且白名单非空 → 403）</item>
    /// <item>资源-动作码命中检查</item>
    /// </list>
    ///
    /// <para>键格式（与 <c>PermissionFilter</c> 保持一致）：<c>CLIENT:&lt;clientId&gt;:clientGrant</c>。</para>
    /// </summary>
    public static class ClientPermissionCache
    {
        public const string Key = "clientGrant";
        public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(8);

        /// <summary>
        /// 读 <see cref="ClientGrant"/>，展开为权限信息（码集合 + 接入控制），写入缓存。
        /// </summary>
        public static async Task RefreshAsync(ICacheClient cache, ClientGrantApiService grantApi, string corpId, string clientId)
        {
            var grant = await grantApi.GetActiveByClientIdAsync(clientId);
            Apply(cache, grant, clientId, clientEnabled: true);
        }

        /// <summary>
        /// 同时读取 <see cref="Client"/> 和 <see cref="ClientGrant"/>，以 Client 的启用/删除状态为准刷新缓存。
        /// </summary>
        public static async Task RefreshAsync(
            ICacheClient cache,
            ClientGrantApiService grantApi,
            ClientApiService clientApi,
            string corpId,
            string clientId)
        {
            var client = await clientApi.GetAsync(clientId);
            if (client == null || client.CorpId != corpId || client.DeleteFlag || !client.Enabled)
            {
                Apply(cache, null, clientId, clientEnabled: false);
                return;
            }

            var grant = await grantApi.GetActiveByClientIdAsync(clientId);
            Apply(cache, grant, clientId, client.Enabled);
        }

        /// <summary>
        /// 同步版本。grant 为 null 时写入"禁用 + 无码 + 无 IP 限制"的状态，等同于完全拒绝。
        /// </summary>
        /// <param name="clientEnabled">Client 自身的 Enabled 状态；false 时即使 grant 有效也拒绝。</param>
        public static void Apply(ICacheClient cache, ClientGrant? grant, string clientId, bool clientEnabled = true)
        {
            var info = BuildInfo(grant);
            if (!clientEnabled)
            {
                info.ClientEnabled = false;
            }
            cache.Set(Key, info, CacheScope.Client, clientId,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = DefaultTtl });
        }

        /// <summary>移除某 Client 的缓存（删除/禁用时用）。</summary>
        public static void Evict(ICacheClient cache, string clientId)
        {
            cache.Remove(Key, CacheScope.Client, clientId);
        }

        private static ClientPermissionInfo BuildInfo(ClientGrant? grant)
        {
            if (grant == null)
            {
                return new ClientPermissionInfo
                {
                    ClientEnabled = false,
                    GrantEnabled = false,
                    Codes = new List<string>(),
                    IpWhitelist = new List<string>(),
                };
            }

            var codes = new List<string>();
            if (grant.Enabled)
            {
                if (string.Equals(grant.ApiScope, "all", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var r in Resources.All)
                    {
                        codes.Add($"{r}:read");
                        codes.Add($"{r}:add");
                        codes.Add($"{r}:edit");
                        codes.Add($"{r}:delete");
                        codes.Add($"{r}:import");
                    }
                }
                else
                {
                    foreach (var ra in grant.ResourceActions)
                    {
                        if (string.IsNullOrWhiteSpace(ra.Resource))
                        {
                            continue;
                        }
                        var op = (Operation)ra.Actions;
                        if (op.HasFlag(Operation.Read))   codes.Add($"{ra.Resource}:read");
                        if (op.HasFlag(Operation.Add))    codes.Add($"{ra.Resource}:add");
                        if (op.HasFlag(Operation.Edit))   codes.Add($"{ra.Resource}:edit");
                        if (op.HasFlag(Operation.Delete)) codes.Add($"{ra.Resource}:delete");
                        if (op.HasFlag(Operation.Import)) codes.Add($"{ra.Resource}:import");
                    }
                }
            }

            return new ClientPermissionInfo
            {
                ClientEnabled = true,  // 由调用方确认 Client 自身启用后传入 true
                GrantEnabled = grant.Enabled,
                Codes = codes,
                IpWhitelist = grant.IpWhitelist ?? new List<string>(),
            };
        }
    }

    /// <summary>
    /// 缓存中存储的 Client 权限与接入控制信息。
    /// </summary>
    public class ClientPermissionInfo
    {
        /// <summary>Client 自身是否启用（false 直接拒绝）。</summary>
        public bool ClientEnabled { get; set; }

        /// <summary>ClientGrant 是否启用（false 直接拒绝）。</summary>
        public bool GrantEnabled { get; set; }

        /// <summary>已展开为 <c>{resource}:{action}</c> 形式的权限码集合。</summary>
        public List<string> Codes { get; set; } = new();

        /// <summary>IP 白名单；空集合表示不限制。</summary>
        public List<string> IpWhitelist { get; set; } = new();
    }
}
