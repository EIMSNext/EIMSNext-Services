using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    public class ExportLog : CorpEntityBase
    {
        public ExportType ExportType { get; set; }

        public ExportFormat RequestedFormat { get; set; } = ExportFormat.Csv;

        public ExportFormat ActualFormat { get; set; } = ExportFormat.Csv;

        public ExportLogStatus Status { get; set; } = ExportLogStatus.Pending;

        public string? ColumnsJson { get; set; }

        public string? FilterJson { get; set; }

        public string? DedupKey { get; set; }

        public long TotalCount { get; set; }

        public string? FileName { get; set; }

        public string? DownloadUrl { get; set; }

        public string? ErrorMessage { get; set; }

        public long? FinishTime { get; set; }
    }

    public enum ExportType
    {
        AuditLogin = 0,
        AuditLog = 1,
        FormData = 2,
    }

    public enum ExportFormat
    {
        Csv = 0,
        Excel = 1,
    }

    public enum ExportLogStatus
    {
        Pending = 0,
        Processing = 1,
        Succeeded = 2,
        Failed = 3,
    }

    public enum ExportColumnType
    {
        String = 0,
        Number = 1,
        Date = 2,
    }
}
