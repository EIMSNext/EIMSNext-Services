using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// Webhook 字段别名配置请求
    /// </summary>
    public class WebhookAliasRequest : RequestBase
    {
        public string AppId { get; set; } = string.Empty;

        public string FormId { get; set; } = string.Empty;

        public List<FieldAliasItem> FieldAlias { get; set; } = [];
    }
}
