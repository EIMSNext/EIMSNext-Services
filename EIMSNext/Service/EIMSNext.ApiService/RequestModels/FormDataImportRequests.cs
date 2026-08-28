using System.Dynamic;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    public class FormDataImportPreviewResponse
    {
        public List<FormDataImportSheetPreview> Sheets { get; set; } = [];
    }

    public class FormDataImportSheetPreview
    {
        public string Name { get; set; } = string.Empty;

        public int RowCount { get; set; }

        public int ColumnCount { get; set; }

        public List<List<string>> Rows { get; set; } = [];
    }

    public class FormDataImportStartRequest
    {
        public string AppId { get; set; } = string.Empty;

        public string FormId { get; set; } = string.Empty;

        public string? PermissionGroupId { get; set; }

        public FormDataImportMode Mode { get; set; } = FormDataImportMode.AddOnly;

        public bool TriggerValidation { get; set; }

        public bool TriggerWorkflow { get; set; }

        public string SheetName { get; set; } = string.Empty;

        public int HeaderRowIndex { get; set; } = 1;

        public string? MatchField { get; set; }

        public List<FormDataImportMappingItem> Mappings { get; set; } = [];
    }

    public class FormDataImportMappingItem
    {
        public int ColumnIndex { get; set; }

        public string Header { get; set; } = string.Empty;

        public string Field { get; set; } = string.Empty;

        public string FieldTitle { get; set; } = string.Empty;

        public string FieldType { get; set; } = string.Empty;
    }

    public class FormDataImportStartResponse
    {
        public string TaskId { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    public class FormDataImportStatusResponse
    {
        public string TaskId { get; set; } = string.Empty;

        public FormDataImportStatus Status { get; set; }

        public long TotalCount { get; set; }

        public long ProcessedCount { get; set; }

        public long AddCount { get; set; }

        public long UpdateCount { get; set; }

        public long FailedCount { get; set; }

        public string? ErrorMessage { get; set; }

        public string? ErrorReportDownloadUrl { get; set; }

        public bool CanEditErrors { get; set; }

        public int EditableErrorRowCount { get; set; }
    }

    public class FormDataImportEditableErrorsResponse
    {
        public List<FormDataImportEditableErrorRow> Rows { get; set; } = [];
    }

    public class FormDataImportRetryRequest
    {
        public List<FormDataImportCorrectionRow> Rows { get; set; } = [];
    }

    public class FormDataImportRetryResponse
    {
        public string TaskId { get; set; } = string.Empty;

        public long AddCount { get; set; }

        public long UpdateCount { get; set; }

        public long FailedCount { get; set; }

        public List<FormDataImportEditableErrorRow> Rows { get; set; } = [];
    }

    public class FormDataImportEditableErrorRow
    {
        public int RecordIndex { get; set; }

        public int StartRowNumber { get; set; }

        public int? EndRowNumber { get; set; }

        public string? DataId { get; set; }

        public ExpandoObject Data { get; set; } = new();

        public List<FormDataImportCellError> Errors { get; set; } = [];
    }

    public class FormDataImportCorrectionRow
    {
        public string? DataId { get; set; }

        public ExpandoObject Data { get; set; } = new();
    }

    public class FormDataImportCellError
    {
        public string? Field { get; set; }

        public string? FieldTitle { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
