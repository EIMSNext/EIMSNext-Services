using EIMSNext.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// Webhook 字段别名配置请求
    /// </summary>
    public class WebhookAliasRequest : RequestBase
    {
        /// <summary>应用 ID。</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>表单 ID。</summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>字段别名列表。</summary>
        public List<FieldAliasItem> FieldAlias { get; set; } = [];
    }
}
