using EIMSNext.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 客户端授权的 OData 请求模型。
    /// 字段与 <see cref="ClientGrant"/> 实体一致。
    /// </summary>
    public class ClientGrantRequest : RequestBase
    {
        /// <summary>客户端 ID。</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>授权名称。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>应用范围。</summary>
        public string AppScope { get; set; } = "all";

        /// <summary>应用 ID 列表。</summary>
        public List<string> AppIds { get; set; } = new();

        /// <summary>接口范围。</summary>
        public string ApiScope { get; set; } = "all";

        /// <summary>资源操作授权列表。</summary>
        public List<ResourceActionGrant> ResourceActions { get; set; } = new();

        /// <summary>IP 白名单。</summary>
        public List<string> IpWhitelist { get; set; } = new();

        /// <summary>是否启用。</summary>
        public bool Enabled { get; set; } = true;
    }
}
