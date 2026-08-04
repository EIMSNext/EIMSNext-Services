using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单数据导入日志。
    /// </summary>
    public class FormDataImportLog : CorpEntityBase
    {
        /// <summary>
        /// 应用ID。
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 表单ID。
        /// </summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// 表单名称快照。
        /// </summary>
        public string? FormName { get; set; }

        /// <summary>
        /// 授权组ID。
        /// </summary>
        public string? AuthGroupId { get; set; }

        /// <summary>
        /// 任务创建时表单是否启用流程。
        /// </summary>
        public bool FormUsingWorkflow { get; set; }

        /// <summary>
        /// 导入模式。
        /// </summary>
        public FormDataImportMode Mode { get; set; }

        /// <summary>
        /// 是否触发表单数据校验。
        /// </summary>
        public bool TriggerValidation { get; set; }

        /// <summary>
        /// 是否触发流程。仅流程表单有效。
        /// </summary>
        public bool TriggerWorkflow { get; set; }

        /// <summary>
        /// 后台任务实际执行的数据动作。
        /// </summary>
        public DataAction ImportAction { get; set; } = DataAction.Submit;

        /// <summary>
        /// 导入状态。
        /// </summary>
        public FormDataImportStatus Status { get; set; } = FormDataImportStatus.Pending;

        /// <summary>
        /// 更新/新增更新模式使用的匹配字段。
        /// </summary>
        public string? MatchField { get; set; }

        /// <summary>
        /// 工作表名称。
        /// </summary>
        public string SheetName { get; set; } = string.Empty;

        /// <summary>
        /// 标题行序号，1-based。
        /// </summary>
        public int HeaderRowIndex { get; set; }

        /// <summary>
        /// 源文件名。
        /// </summary>
        public string SourceFileName { get; set; } = string.Empty;

        /// <summary>
        /// 源文件对象存储 Key。
        /// </summary>
        public string SourceObjectKey { get; set; } = string.Empty;

        /// <summary>
        /// 源文件大小。
        /// </summary>
        public long SourceFileSize { get; set; }

        /// <summary>
        /// 表单字段快照 JSON。
        /// </summary>
        public string FieldSnapshotJson { get; set; } = string.Empty;

        /// <summary>
        /// 字段映射 JSON。
        /// </summary>
        public string MappingJson { get; set; } = string.Empty;

        /// <summary>
        /// 导入任务可访问的数据范围过滤器 JSON。
        /// </summary>
        public string? DataScopeFilterJson { get; set; }

        /// <summary>
        /// 总记录数。
        /// </summary>
        public long TotalCount { get; set; }

        /// <summary>
        /// 已处理记录数。
        /// </summary>
        public long ProcessedCount { get; set; }

        /// <summary>
        /// 新增成功数。
        /// </summary>
        public long AddCount { get; set; }

        /// <summary>
        /// 更新成功数。
        /// </summary>
        public long UpdateCount { get; set; }

        /// <summary>
        /// 失败记录数。
        /// </summary>
        public long FailedCount { get; set; }

        /// <summary>
        /// 错误报告文件名。
        /// </summary>
        public string? ErrorReportFileName { get; set; }

        /// <summary>
        /// 错误报告对象存储 Key。
        /// </summary>
        public string? ErrorReportObjectKey { get; set; }

        /// <summary>
        /// 错误报告下载地址。
        /// </summary>
        public string? ErrorReportDownloadUrl { get; set; }

        /// <summary>
        /// 少量可在线编辑失败数据 JSON。
        /// </summary>
        public string? EditableErrorRowsJson { get; set; }

        /// <summary>
        /// 少量可在线编辑失败数据对象存储 Key。
        /// </summary>
        public string? EditableErrorRowsObjectKey { get; set; }

        /// <summary>
        /// 可在线编辑失败数据条数。
        /// </summary>
        public int EditableErrorRowCount { get; set; }

        /// <summary>
        /// 任务级错误信息。
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 重试次数。
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// 开始时间。
        /// </summary>
        public long? StartTime { get; set; }

        /// <summary>
        /// 处理中租约过期时间。用于 worker 崩溃后的重投恢复。
        /// </summary>
        public long? ProcessingExpireTime { get; set; }

        /// <summary>
        /// 完成时间。
        /// </summary>
        public long? FinishTime { get; set; }
    }

    /// <summary>
    /// 表单数据导入模式。
    /// </summary>
    public enum FormDataImportMode
    {
        /// <summary>
        /// 仅新增。
        /// </summary>
        AddOnly = 0,
        /// <summary>
        /// 仅更新。
        /// </summary>
        UpdateOnly = 1,
        /// <summary>
        /// 更新和新增。
        /// </summary>
        Upsert = 2
    }

    /// <summary>
    /// 表单数据导入状态。
    /// </summary>
    public enum FormDataImportStatus
    {
        /// <summary>
        /// 待处理。
        /// </summary>
        Pending = 0,
        /// <summary>
        /// 处理中。
        /// </summary>
        Processing = 1,
        /// <summary>
        /// 全部成功。
        /// </summary>
        Succeeded = 2,
        /// <summary>
        /// 完成但存在行级错误。
        /// </summary>
        CompletedWithErrors = 3,
        /// <summary>
        /// 任务级失败。
        /// </summary>
        Failed = 4
    }

    /// <summary>
    /// 导入行动作。
    /// </summary>
    public enum FormDataImportRowAction
    {
        /// <summary>
        /// 新增。
        /// </summary>
        Add = 0,
        /// <summary>
        /// 更新。
        /// </summary>
        Update = 1
    }
}
