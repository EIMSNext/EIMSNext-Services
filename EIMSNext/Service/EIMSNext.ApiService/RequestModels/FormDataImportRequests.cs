using System.Dynamic;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 表单数据导入预览响应。
    /// </summary>
    public class FormDataImportPreviewResponse
    {
        /// <summary>
        /// 获取或设置工作表预览列表。
        /// </summary>
        public List<FormDataImportSheetPreview> Sheets { get; set; } = [];
    }

    /// <summary>
    /// 表单数据导入工作表预览。
    /// </summary>
    public class FormDataImportSheetPreview
    {
        /// <summary>
        /// 获取或设置工作表名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置行数。
        /// </summary>
        public int RowCount { get; set; }

        /// <summary>
        /// 获取或设置列数。
        /// </summary>
        public int ColumnCount { get; set; }

        /// <summary>
        /// 获取或设置行数据。
        /// </summary>
        public List<List<string>> Rows { get; set; } = [];
    }

    /// <summary>
    /// 表单数据导入开始请求。
    /// </summary>
    public class FormDataImportStartRequest
    {
        /// <summary>
        /// 获取或设置应用 ID。
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置表单 ID。
        /// </summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置权限组 ID。
        /// </summary>
        public string? PermissionGroupId { get; set; }

        /// <summary>
        /// 获取或设置导入模式。
        /// </summary>
        public FormDataImportMode Mode { get; set; } = FormDataImportMode.AddOnly;

        /// <summary>
        /// 获取或设置是否触发校验。
        /// </summary>
        public bool TriggerValidation { get; set; }

        /// <summary>
        /// 获取或设置是否触发工作流。
        /// </summary>
        public bool TriggerWorkflow { get; set; }

        /// <summary>
        /// 获取或设置工作表名称。
        /// </summary>
        public string SheetName { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置表头行索引。
        /// </summary>
        public int HeaderRowIndex { get; set; } = 1;

        /// <summary>
        /// 获取或设置匹配字段。
        /// </summary>
        public string? MatchField { get; set; }

        /// <summary>
        /// 获取或设置字段映射列表。
        /// </summary>
        public List<FormDataImportMappingItem> Mappings { get; set; } = [];
    }

    /// <summary>
    /// 表单数据导入字段映射项。
    /// </summary>
    public class FormDataImportMappingItem
    {
        /// <summary>
        /// 获取或设置列索引。
        /// </summary>
        public int ColumnIndex { get; set; }

        /// <summary>
        /// 获取或设置表头。
        /// </summary>
        public string Header { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置字段路径。
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置字段标题。
        /// </summary>
        public string FieldTitle { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置字段类型。
        /// </summary>
        public string FieldType { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表单数据导入开始响应。
    /// </summary>
    public class FormDataImportStartResponse
    {
        /// <summary>
        /// 获取或设置任务 ID。
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置提示消息。
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表单数据导入状态响应。
    /// </summary>
    public class FormDataImportStatusResponse
    {
        /// <summary>
        /// 获取或设置任务 ID。
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置导入状态。
        /// </summary>
        public FormDataImportStatus Status { get; set; }

        /// <summary>
        /// 获取或设置记录总数。
        /// </summary>
        public long TotalCount { get; set; }

        /// <summary>
        /// 获取或设置已处理记录数。
        /// </summary>
        public long ProcessedCount { get; set; }

        /// <summary>
        /// 获取或设置新增记录数。
        /// </summary>
        public long AddCount { get; set; }

        /// <summary>
        /// 获取或设置更新记录数。
        /// </summary>
        public long UpdateCount { get; set; }

        /// <summary>
        /// 获取或设置失败记录数。
        /// </summary>
        public long FailedCount { get; set; }

        /// <summary>
        /// 获取或设置错误消息。
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 获取或设置错误报告下载地址。
        /// </summary>
        public string? ErrorReportDownloadUrl { get; set; }

        /// <summary>
        /// 获取或设置是否可编辑错误。
        /// </summary>
        public bool CanEditErrors { get; set; }

        /// <summary>
        /// 获取或设置可编辑错误行数。
        /// </summary>
        public int EditableErrorRowCount { get; set; }
    }

    /// <summary>
    /// 表单数据导入可编辑错误响应。
    /// </summary>
    public class FormDataImportEditableErrorsResponse
    {
        /// <summary>
        /// 获取或设置可编辑错误行列表。
        /// </summary>
        public List<FormDataImportEditableErrorRow> Rows { get; set; } = [];
    }

    /// <summary>
    /// 表单数据导入重试请求。
    /// </summary>
    public class FormDataImportRetryRequest
    {
        /// <summary>
        /// 获取或设置修正行列表。
        /// </summary>
        public List<FormDataImportCorrectionRow> Rows { get; set; } = [];
    }

    /// <summary>
    /// 表单数据导入重试响应。
    /// </summary>
    public class FormDataImportRetryResponse
    {
        /// <summary>
        /// 获取或设置任务 ID。
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置新增记录数。
        /// </summary>
        public long AddCount { get; set; }

        /// <summary>
        /// 获取或设置更新记录数。
        /// </summary>
        public long UpdateCount { get; set; }

        /// <summary>
        /// 获取或设置失败记录数。
        /// </summary>
        public long FailedCount { get; set; }

        /// <summary>
        /// 获取或设置可编辑错误行列表。
        /// </summary>
        public List<FormDataImportEditableErrorRow> Rows { get; set; } = [];
    }

    /// <summary>
    /// 表单数据导入可编辑错误行。
    /// </summary>
    public class FormDataImportEditableErrorRow
    {
        /// <summary>
        /// 获取或设置记录索引。
        /// </summary>
        public int RecordIndex { get; set; }

        /// <summary>
        /// 获取或设置起始行号。
        /// </summary>
        public int StartRowNumber { get; set; }

        /// <summary>
        /// 获取或设置结束行号。
        /// </summary>
        public int? EndRowNumber { get; set; }

        /// <summary>
        /// 获取或设置数据 ID。
        /// </summary>
        public string? DataId { get; set; }

        /// <summary>
        /// 获取或设置数据对象。
        /// </summary>
        public ExpandoObject Data { get; set; } = new();

        /// <summary>
        /// 获取或设置单元格错误列表。
        /// </summary>
        public List<FormDataImportCellError> Errors { get; set; } = [];
    }

    /// <summary>
    /// 表单数据导入修正行。
    /// </summary>
    public class FormDataImportCorrectionRow
    {
        /// <summary>
        /// 获取或设置数据 ID。
        /// </summary>
        public string? DataId { get; set; }

        /// <summary>
        /// 获取或设置数据对象。
        /// </summary>
        public ExpandoObject Data { get; set; } = new();
    }

    /// <summary>
    /// 表单数据导入单元格错误。
    /// </summary>
    public class FormDataImportCellError
    {
        /// <summary>
        /// 获取或设置字段路径。
        /// </summary>
        public string? Field { get; set; }

        /// <summary>
        /// 获取或设置字段标题。
        /// </summary>
        public string? FieldTitle { get; set; }

        /// <summary>
        /// 获取或设置错误消息。
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
