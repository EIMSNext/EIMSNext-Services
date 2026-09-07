using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 导出列定义。
    /// </summary>
    public class ExportColumn
    {
        /// <summary>字段键。</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>表头。</summary>
        public string Header { get; set; } = string.Empty;

        /// <summary>列类型。</summary>
        public ExportColumnType Type { get; set; } = ExportColumnType.String;
    }

    /// <summary>
    /// 身份登录审计导出请求。
    /// </summary>
    public class IdentityLoginAuditExportRequest
    {
        /// <summary>导出格式。</summary>
        public ExportFormat Format { get; set; } = ExportFormat.Csv;

        /// <summary>导出列列表。</summary>
        public List<ExportColumn> Columns { get; set; } = [];

        /// <summary>用户名。</summary>
        public string? UserName { get; set; }

        /// <summary>起始时间。</summary>
        public long? StartTime { get; set; }

        /// <summary>结束时间。</summary>
        public long? EndTime { get; set; }
    }

    /// <summary>
    /// 审计日志导出请求。
    /// </summary>
    public class AuditLogExportRequest
    {
        /// <summary>导出格式。</summary>
        public ExportFormat Format { get; set; } = ExportFormat.Csv;

        /// <summary>导出列列表。</summary>
        public List<ExportColumn> Columns { get; set; } = [];

        /// <summary>实体类型。</summary>
        public string? EntityType { get; set; }

        /// <summary>操作类型。</summary>
        public string? Action { get; set; }

        /// <summary>操作者名称。</summary>
        public string? OperatorName { get; set; }

        /// <summary>起始时间。</summary>
        public long? StartTime { get; set; }

        /// <summary>结束时间。</summary>
        public long? EndTime { get; set; }
    }

    /// <summary>
    /// 表单数据导出请求。
    /// </summary>
    public class FormDataExportRequest
    {
        /// <summary>导出格式。</summary>
        public ExportFormat Format { get; set; } = ExportFormat.Csv;

        /// <summary>导出列列表。</summary>
        public List<ExportColumn> Columns { get; set; } = [];

        /// <summary>表单 ID。</summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>动态过滤条件。</summary>
        public DynamicFilter? Filter { get; set; }

        /// <summary>权限组 ID。</summary>
        public string? PermissionGroupId { get; set; }

        /// <summary>关键字。</summary>
        public string? Keyword { get; set; }

        /// <summary>参与搜索的字段列表。</summary>
        public List<string>? SearchFields { get; set; }

        /// <summary>是否包含逻辑删除数据。</summary>
        public bool IncludeDeleted { get; set; }
    }

    /// <summary>
    /// 导出响应。
    /// </summary>
    public class ExportResponse
    {
        /// <summary>任务 ID。</summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>是否重复提交。</summary>
        public bool IsDuplicate { get; set; }

        /// <summary>实际导出格式。</summary>
        public ExportFormat ActualFormat { get; set; }

        /// <summary>提示消息。</summary>
        public string? Message { get; set; }
    }
}
