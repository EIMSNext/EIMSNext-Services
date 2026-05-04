using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// Webhook 字段别名配置
    /// </summary>
    public class WebhookAlias : CorpEntityBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// 字段别名配置
        /// </summary>
        public List<FieldAliasItem> FieldAlias { get; set; } = [];
    }

    /// <summary>
    /// 字段别名项
    /// </summary>
    public class FieldAliasItem
    {
        /// <summary>
        /// 字段ID
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// 字段别名
        /// </summary>
        public string Alias { get; set; } = string.Empty;

        /// <summary>
        /// 子字段别名，仅子表单字段使用
        /// </summary>
        public List<FieldAliasItem>? Children { get; set; }
    }
}
