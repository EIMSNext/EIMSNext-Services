using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 导出日志。
    /// </summary>
    public class ExportLog : CorpEntityBase
    {
        /// <summary>
        /// 导出类型。
        /// </summary>
        public ExportType ExportType { get; set; }

        /// <summary>
        /// 请求的导出格式。
        /// </summary>
        public ExportFormat RequestedFormat { get; set; } = ExportFormat.Csv;

        /// <summary>
        /// 实际导出格式。
        /// </summary>
        public ExportFormat ActualFormat { get; set; } = ExportFormat.Csv;

        /// <summary>
        /// 导出状态。
        /// </summary>
        public ExportLogStatus Status { get; set; } = ExportLogStatus.Pending;

        /// <summary>
        /// 导出列配置 JSON。
        /// </summary>
        public string? ColumnsJson { get; set; }

        /// <summary>
        /// 过滤条件 JSON。
        /// </summary>
        public string? FilterJson { get; set; }

        /// <summary>
        /// 幂等去重键。
        /// </summary>
        public string? DedupKey { get; set; }

        /// <summary>
        /// 导出总数。
        /// </summary>
        public long TotalCount { get; set; }

        /// <summary>
        /// 文件名。
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// 下载地址。
        /// </summary>
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// 错误信息。
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 完成时间。
        /// </summary>
        public long? FinishTime { get; set; }
    }

    /// <summary>
    /// 导出类型。
    /// </summary>
    public enum ExportType
    {
        /// <summary>
        /// 登录日志。
        /// </summary>
        AuditLogin = 0,
        /// <summary>
        /// 审计日志。
        /// </summary>
        AuditLog = 1,
        /// <summary>
        /// 表单数据。
        /// </summary>
        FormData = 2,
    }

    /// <summary>
    /// 导出格式。
    /// </summary>
    public enum ExportFormat
    {
        /// <summary>
        /// CSV。
        /// </summary>
        Csv = 0,
        /// <summary>
        /// Excel。
        /// </summary>
        Excel = 1,
    }

    /// <summary>
    /// 导出日志状态。
    /// </summary>
    public enum ExportLogStatus
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
        /// 成功。
        /// </summary>
        Succeeded = 2,
        /// <summary>
        /// 失败。
        /// </summary>
        Failed = 3,
    }

    /// <summary>
    /// 导出列值类型。
    /// </summary>
    public enum ExportColumnType
    {
        /// <summary>
        /// 字符串。
        /// </summary>
        String = 0,
        /// <summary>
        /// 数值。
        /// </summary>
        Number = 1,
        /// <summary>
        /// 日期。
        /// </summary>
        Date = 2,
    }
}
