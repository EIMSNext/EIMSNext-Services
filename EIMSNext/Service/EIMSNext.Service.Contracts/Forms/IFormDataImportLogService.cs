using EIMSNext.Core.Services;
using EIMSNext.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IFormDataImportLogService : IService<FormDataImportLog>
    {
        Task<bool> TryMarkProcessingAsync(string id, int retryCount);

        Task MarkProcessingAsync(string id, long totalCount);

        Task UpdateProgressAsync(string id, long processedCount, long addCount, long updateCount, long failedCount);

        Task MarkSucceededAsync(string id, long totalCount, long addCount, long updateCount);

        Task MarkCompletedWithErrorsAsync(
            string id,
            long totalCount,
            long addCount,
            long updateCount,
            long failedCount,
            string errorReportFileName,
            string errorReportObjectKey,
            string errorReportDownloadUrl,
            string? editableErrorRowsJson,
            string? editableErrorRowsObjectKey,
            int editableErrorRowCount);

        Task MarkFailedAsync(
            string id,
            string errorMessage,
            string? errorReportFileName = null,
            string? errorReportObjectKey = null,
            string? errorReportDownloadUrl = null);

        Task MarkCorrectionResultAsync(
            string id,
            long totalCount,
            long addCount,
            long updateCount,
            long failedCount,
            string? editableErrorRowsJson,
            string? editableErrorRowsObjectKey,
            int editableErrorRowCount);

        Task UpdateEditableErrorsAsync(string id, string? editableErrorRowsJson, string? editableErrorRowsObjectKey, int editableErrorRowCount);

        Task IncrementRetryAsync(string id);
    }
}
