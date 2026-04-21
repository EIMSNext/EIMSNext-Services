using EIMSNext.Core.Query;
using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    public class ExportColumn
    {
        public string Key { get; set; } = string.Empty;

        public string Header { get; set; } = string.Empty;

        public ExportColumnType Type { get; set; } = ExportColumnType.String;
    }

    public class AuditLoginExportRequest
    {
        public ExportFormat Format { get; set; } = ExportFormat.Csv;

        public List<ExportColumn> Columns { get; set; } = [];

        public string? UserName { get; set; }

        public long? StartTime { get; set; }

        public long? EndTime { get; set; }
    }

    public class AuditLogExportRequest
    {
        public ExportFormat Format { get; set; } = ExportFormat.Csv;

        public List<ExportColumn> Columns { get; set; } = [];

        public string? EntityType { get; set; }

        public string? Action { get; set; }

        public string? OperatorName { get; set; }

        public long? StartTime { get; set; }

        public long? EndTime { get; set; }
    }

    public class FormDataExportRequest
    {
        public ExportFormat Format { get; set; } = ExportFormat.Csv;

        public List<ExportColumn> Columns { get; set; } = [];

        public string FormId { get; set; } = string.Empty;

        public DynamicFilter? Filter { get; set; }

        public string? AuthGroupId { get; set; }
    }

    public class ExportResponse
    {
        public string TaskId { get; set; } = string.Empty;

        public bool IsDuplicate { get; set; }

        public ExportFormat ActualFormat { get; set; }

        public string? Message { get; set; }
    }
}
