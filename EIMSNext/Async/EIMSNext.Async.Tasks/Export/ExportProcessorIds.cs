using EIMSNext.Entities;

namespace EIMSNext.Async.Tasks.Export
{
    public static class ExportProcessorIds
    {
        public const string AuditLog = "auditlogexportor";
        public const string IdentityLoginAudit = "auditloginexportor";
        public const string FormData = "formdataexportor";

        public static string FromExportType(ExportType exportType)
        {
            return exportType switch
            {
                ExportType.IdentityLoginAudit => IdentityLoginAudit,
                ExportType.AuditLog => AuditLog,
                ExportType.FormData => FormData,
                _ => throw new NotSupportedException($"Unsupported export type: {exportType}"),
            };
        }
    }
}
