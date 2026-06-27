using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 客户端授权的 OData 请求模型。
    /// 字段与 <see cref="ClientGrant"/> 实体一致。
    /// </summary>
    public class ClientGrantRequest : RequestBase
    {
        public string ClientId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AppScope { get; set; } = "all";
        public List<string> AppIds { get; set; } = new();
        public string ApiScope { get; set; } = "all";
        public List<ResourceActionGrant> ResourceActions { get; set; } = new();
        public List<string> IpWhitelist { get; set; } = new();
        public bool Enabled { get; set; } = true;
    }
}
