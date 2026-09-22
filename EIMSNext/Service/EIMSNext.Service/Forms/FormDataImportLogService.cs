using EIMSNext.Common.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Service
{
    public class FormDataImportLogService(IResolver resolver) : EntityServiceBase<FormDataImportLog>(resolver), IFormDataImportLogService
    {
        private const long ProcessingLeaseMs = 30L * 60 * 1000;

        public async Task<bool> TryMarkProcessingAsync(string id, int retryCount)
        {
            var now = DateTime.UtcNow.ToTimeStampMs();
            var affected = await Repository.UpdateManyAsync(
                x => x.Id == id
                    && x.RetryCount == retryCount
                    && x.Status == FormDataImportStatus.Pending,
                setters => setters
                    .SetProperty(x => x.Status, FormDataImportStatus.Processing)
                    .SetProperty(x => x.TotalCount, 0L)
                    .SetProperty(x => x.ProcessedCount, 0L)
                    .SetProperty(x => x.AddCount, 0L)
                    .SetProperty(x => x.UpdateCount, 0L)
                    .SetProperty(x => x.FailedCount, 0L)
                    .SetProperty(x => x.StartTime, now)
                    .SetProperty(x => x.FinishTime, (long?)null)
                    .SetProperty(x => x.ProcessingExpireTime, now + ProcessingLeaseMs)
                    .SetProperty(x => x.ErrorMessage, (string?)null));

            return affected == 1;
        }

        public Task MarkProcessingAsync(string id, long totalCount)
        {
            var now = DateTime.UtcNow.ToTimeStampMs();
            return Repository.UpdateAsync(
                id,
                setters => setters
                    .SetProperty(x => x.Status, FormDataImportStatus.Processing)
                    .SetProperty(x => x.TotalCount, totalCount)
                    .SetProperty(x => x.ProcessedCount, 0L)
                    .SetProperty(x => x.AddCount, 0L)
                    .SetProperty(x => x.UpdateCount, 0L)
                    .SetProperty(x => x.FailedCount, 0L)
                    .SetProperty(x => x.StartTime, now)
                    .SetProperty(x => x.FinishTime, (long?)null)
                    .SetProperty(x => x.ProcessingExpireTime, now + ProcessingLeaseMs)
                    .SetProperty(x => x.ErrorMessage, (string?)null));
        }

        public Task UpdateProgressAsync(string id, long processedCount, long addCount, long updateCount, long failedCount)
        {
            var now = DateTime.UtcNow.ToTimeStampMs();
            return Repository.UpdateAsync(
                id,
                setters => setters
                    .SetProperty(x => x.ProcessedCount, processedCount)
                    .SetProperty(x => x.AddCount, addCount)
                    .SetProperty(x => x.UpdateCount, updateCount)
                    .SetProperty(x => x.FailedCount, failedCount)
                    .SetProperty(x => x.ProcessingExpireTime, now + ProcessingLeaseMs));
        }

        public Task MarkSucceededAsync(string id, long totalCount, long addCount, long updateCount)
        {
            return Repository.UpdateAsync(
                id,
                setters => setters
                    .SetProperty(x => x.Status, FormDataImportStatus.Succeeded)
                    .SetProperty(x => x.TotalCount, totalCount)
                    .SetProperty(x => x.ProcessedCount, totalCount)
                    .SetProperty(x => x.AddCount, addCount)
                    .SetProperty(x => x.UpdateCount, updateCount)
                    .SetProperty(x => x.FailedCount, 0L)
                    .SetProperty(x => x.EditableErrorRowsJson, (string?)null)
                    .SetProperty(x => x.EditableErrorRowsObjectKey, (string?)null)
                    .SetProperty(x => x.EditableErrorRowCount, 0)
                    .SetProperty(x => x.ErrorMessage, (string?)null)
                    .SetProperty(x => x.ProcessingExpireTime, (long?)null)
                    .SetProperty(x => x.FinishTime, DateTime.UtcNow.ToTimeStampMs()));
        }

        public Task MarkCompletedWithErrorsAsync(
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
            int editableErrorRowCount)
        {
            return Repository.UpdateAsync(
                id,
                setters => setters
                    .SetProperty(x => x.Status, FormDataImportStatus.CompletedWithErrors)
                    .SetProperty(x => x.TotalCount, totalCount)
                    .SetProperty(x => x.ProcessedCount, totalCount)
                    .SetProperty(x => x.AddCount, addCount)
                    .SetProperty(x => x.UpdateCount, updateCount)
                    .SetProperty(x => x.FailedCount, failedCount)
                    .SetProperty(x => x.ErrorReportFileName, errorReportFileName)
                    .SetProperty(x => x.ErrorReportObjectKey, errorReportObjectKey)
                    .SetProperty(x => x.ErrorReportDownloadUrl, errorReportDownloadUrl)
                    .SetProperty(x => x.EditableErrorRowsJson, editableErrorRowsJson)
                    .SetProperty(x => x.EditableErrorRowsObjectKey, editableErrorRowsObjectKey)
                    .SetProperty(x => x.EditableErrorRowCount, editableErrorRowCount)
                    .SetProperty(x => x.ErrorMessage, (string?)null)
                    .SetProperty(x => x.ProcessingExpireTime, (long?)null)
                    .SetProperty(x => x.FinishTime, DateTime.UtcNow.ToTimeStampMs()));
        }

        public Task MarkFailedAsync(
            string id,
            string errorMessage,
            string? errorReportFileName = null,
            string? errorReportObjectKey = null,
            string? errorReportDownloadUrl = null)
        {
            return Repository.UpdateAsync(
                id,
                setters => setters
                    .SetProperty(x => x.Status, FormDataImportStatus.Failed)
                    .SetProperty(x => x.ErrorMessage, errorMessage)
                    .SetProperty(x => x.ErrorReportFileName, errorReportFileName)
                    .SetProperty(x => x.ErrorReportObjectKey, errorReportObjectKey)
                    .SetProperty(x => x.ErrorReportDownloadUrl, errorReportDownloadUrl)
                    .SetProperty(x => x.EditableErrorRowsJson, (string?)null)
                    .SetProperty(x => x.EditableErrorRowsObjectKey, (string?)null)
                    .SetProperty(x => x.EditableErrorRowCount, 0)
                    .SetProperty(x => x.ProcessingExpireTime, (long?)null)
                    .SetProperty(x => x.FinishTime, DateTime.UtcNow.ToTimeStampMs()));
        }

        public Task MarkCorrectionResultAsync(
            string id,
            long totalCount,
            long addCount,
            long updateCount,
            long failedCount,
            string? editableErrorRowsJson,
            string? editableErrorRowsObjectKey,
            int editableErrorRowCount)
        {
            var hasErrors = failedCount > 0;
            return Repository.UpdateAsync(
                id,
                setters => setters
                    .SetProperty(x => x.Status, hasErrors ? FormDataImportStatus.CompletedWithErrors : FormDataImportStatus.Succeeded)
                    .SetProperty(x => x.TotalCount, totalCount)
                    .SetProperty(x => x.ProcessedCount, totalCount)
                    .SetProperty(x => x.AddCount, addCount)
                    .SetProperty(x => x.UpdateCount, updateCount)
                    .SetProperty(x => x.FailedCount, failedCount)
                    .SetProperty(x => x.EditableErrorRowsJson, editableErrorRowsJson)
                    .SetProperty(x => x.EditableErrorRowsObjectKey, editableErrorRowsObjectKey)
                    .SetProperty(x => x.EditableErrorRowCount, editableErrorRowCount)
                    .SetProperty(x => x.ErrorMessage, (string?)null)
                    .SetProperty(x => x.ErrorReportFileName, (string?)null)
                    .SetProperty(x => x.ErrorReportObjectKey, (string?)null)
                    .SetProperty(x => x.ErrorReportDownloadUrl, (string?)null)
                    .SetProperty(x => x.ProcessingExpireTime, (long?)null)
                    .SetProperty(x => x.FinishTime, DateTime.UtcNow.ToTimeStampMs()));
        }

        public Task UpdateEditableErrorsAsync(string id, string? editableErrorRowsJson, string? editableErrorRowsObjectKey, int editableErrorRowCount)
        {
            return Repository.UpdateAsync(
                id,
                setters => setters
                    .SetProperty(x => x.EditableErrorRowsJson, editableErrorRowsJson)
                    .SetProperty(x => x.EditableErrorRowsObjectKey, editableErrorRowsObjectKey)
                    .SetProperty(x => x.EditableErrorRowCount, editableErrorRowCount));
        }

        public Task IncrementRetryAsync(string id)
        {
            // EF Core 的 SetProperty 支持表达式自增，等价于 Mongo 的 $inc。
            return Repository.UpdateAsync(
                id,
                setters => setters.SetProperty(x => x.RetryCount, x => x.RetryCount + 1));
        }
    }
}
