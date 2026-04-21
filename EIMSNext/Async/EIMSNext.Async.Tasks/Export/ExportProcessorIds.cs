using EIMSNext.Service.Entities;

namespace EIMSNext.Async.Tasks.Export
{
    public static class ExportProcessorIds
    {
        public const string AuditLog = "auditlogexportor";
        public const string AuditLogin = "auditloginexportor";
        public const string FormData = "formdataexportor";

        public static string FromExportType(ExportType exportType)
        {
            return exportType switch
            {
                ExportType.AuditLogin => AuditLogin,
                ExportType.AuditLog => AuditLog,
                ExportType.FormData => FormData,
                _ => throw new NotSupportedException($"Unsupported export type: {exportType}"),
            };
        }
    }
}
