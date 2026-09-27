using EIMSNext.Common.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class FormDataImportLogService(IResolver resolver) : EntityServiceBase<FormDataImportLog>(resolver), IFormDataImportLogService
    {
        private const long ProcessingLeaseMs = 30L * 60 * 1000;

        public Task<bool> TryMarkProcessingAsync(string id, int retryCount)
        {
            var now = DateTime.UtcNow.ToTimeStampMs();
            var filter = FilterBuilder.And(
                FilterBuilder.Eq(x => x.Id, id),
                FilterBuilder.Eq(x => x.RetryCount, retryCount),
                FilterBuilder.Eq(x => x.Status, FormDataImportStatus.Pending));
            var update = UpdateBuilder
                .Set(x => x.Status, FormDataImportStatus.Processing)
                .Set(x => x.TotalCount, 0)
                .Set(x => x.ProcessedCount, 0)
                .Set(x => x.AddCount, 0)
                .Set(x => x.UpdateCount, 0)
                .Set(x => x.FailedCount, 0)
                .Set(x => x.StartTime, now)
                .Set(x => x.FinishTime, (long?)null)
                .Set(x => x.ProcessingExpireTime, now + ProcessingLeaseMs)
                .Set(x => x.ErrorMessage, (string?)null);

            var result = Repository.UpdateMany(filter, update, upsert: false);
            return Task.FromResult(result.ModifiedCount == 1);
        }

        public Task MarkProcessingAsync(string id, long totalCount)
        {
            var now = DateTime.UtcNow.ToTimeStampMs();
            var update = UpdateBuilder
                .Set(x => x.Status, FormDataImportStatus.Processing)
                .Set(x => x.TotalCount, totalCount)
                .Set(x => x.ProcessedCount, 0)
                .Set(x => x.AddCount, 0)
                .Set(x => x.UpdateCount, 0)
                .Set(x => x.FailedCount, 0)
                .Set(x => x.StartTime, now)
                .Set(x => x.FinishTime, (long?)null)
                .Set(x => x.ProcessingExpireTime, now + ProcessingLeaseMs)
                .Set(x => x.ErrorMessage, (string?)null);

            return Repository.UpdateAsync(id, update, upsert: false);
        }

        public Task UpdateProgressAsync(string id, long processedCount, long addCount, long updateCount, long failedCount)
        {
            var now = DateTime.UtcNow.ToTimeStampMs();
            var update = UpdateBuilder
                .Set(x => x.ProcessedCount, processedCount)
                .Set(x => x.AddCount, addCount)
                .Set(x => x.UpdateCount, updateCount)
                .Set(x => x.FailedCount, failedCount)
                .Set(x => x.ProcessingExpireTime, now + ProcessingLeaseMs);

            return Repository.UpdateAsync(id, update, upsert: false);
        }

        public Task MarkSucceededAsync(string id, long totalCount, long addCount, long updateCount)
        {
            var update = UpdateBuilder
                .Set(x => x.Status, FormDataImportStatus.Succeeded)
                .Set(x => x.TotalCount, totalCount)
                .Set(x => x.ProcessedCount, totalCount)
                .Set(x => x.AddCount, addCount)
                .Set(x => x.UpdateCount, updateCount)
                .Set(x => x.FailedCount, 0)
                .Set(x => x.EditableErrorRowsJson, (string?)null)
                .Set(x => x.EditableErrorRowsObjectKey, (string?)null)
                .Set(x => x.EditableErrorRowCount, 0)
                .Set(x => x.ErrorMessage, (string?)null)
                .Set(x => x.ProcessingExpireTime, (long?)null)
                .Set(x => x.FinishTime, DateTime.UtcNow.ToTimeStampMs());

            return Repository.UpdateAsync(id, update, upsert: false);
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
            var update = UpdateBuilder
                .Set(x => x.Status, FormDataImportStatus.CompletedWithErrors)
                .Set(x => x.TotalCount, totalCount)
                .Set(x => x.ProcessedCount, totalCount)
                .Set(x => x.AddCount, addCount)
                .Set(x => x.UpdateCount, updateCount)
                .Set(x => x.FailedCount, failedCount)
                .Set(x => x.ErrorReportFileName, errorReportFileName)
                .Set(x => x.ErrorReportObjectKey, errorReportObjectKey)
                .Set(x => x.ErrorReportDownloadUrl, errorReportDownloadUrl)
                .Set(x => x.EditableErrorRowsJson, editableErrorRowsJson)
                .Set(x => x.EditableErrorRowsObjectKey, editableErrorRowsObjectKey)
                .Set(x => x.EditableErrorRowCount, editableErrorRowCount)
                .Set(x => x.ErrorMessage, (string?)null)
                .Set(x => x.ProcessingExpireTime, (long?)null)
                .Set(x => x.FinishTime, DateTime.UtcNow.ToTimeStampMs());

            return Repository.UpdateAsync(id, update, upsert: false);
        }

        public Task MarkFailedAsync(
            string id,
            string errorMessage,
            string? errorReportFileName = null,
            string? errorReportObjectKey = null,
            string? errorReportDownloadUrl = null)
        {
            var update = UpdateBuilder
                .Set(x => x.Status, FormDataImportStatus.Failed)
                .Set(x => x.ErrorMessage, errorMessage)
                .Set(x => x.ErrorReportFileName, errorReportFileName)
                .Set(x => x.ErrorReportObjectKey, errorReportObjectKey)
                .Set(x => x.ErrorReportDownloadUrl, errorReportDownloadUrl)
                .Set(x => x.EditableErrorRowsJson, (string?)null)
                .Set(x => x.EditableErrorRowsObjectKey, (string?)null)
                .Set(x => x.EditableErrorRowCount, 0)
                .Set(x => x.ProcessingExpireTime, (long?)null)
                .Set(x => x.FinishTime, DateTime.UtcNow.ToTimeStampMs());

            return Repository.UpdateAsync(id, update, upsert: false);
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
            var update = UpdateBuilder
                .Set(x => x.Status, hasErrors ? FormDataImportStatus.CompletedWithErrors : FormDataImportStatus.Succeeded)
                .Set(x => x.TotalCount, totalCount)
                .Set(x => x.ProcessedCount, totalCount)
                .Set(x => x.AddCount, addCount)
                .Set(x => x.UpdateCount, updateCount)
                .Set(x => x.FailedCount, failedCount)
                .Set(x => x.EditableErrorRowsJson, editableErrorRowsJson)
                .Set(x => x.EditableErrorRowsObjectKey, editableErrorRowsObjectKey)
                .Set(x => x.EditableErrorRowCount, editableErrorRowCount)
                .Set(x => x.ErrorMessage, (string?)null)
                .Set(x => x.ErrorReportFileName, (string?)null)
                .Set(x => x.ErrorReportObjectKey, (string?)null)
                .Set(x => x.ErrorReportDownloadUrl, (string?)null)
                .Set(x => x.ProcessingExpireTime, (long?)null)
                .Set(x => x.FinishTime, DateTime.UtcNow.ToTimeStampMs());

            return Repository.UpdateAsync(id, update, upsert: false);
        }

        public Task UpdateEditableErrorsAsync(string id, string? editableErrorRowsJson, string? editableErrorRowsObjectKey, int editableErrorRowCount)
        {
            var update = UpdateBuilder
                .Set(x => x.EditableErrorRowsJson, editableErrorRowsJson)
                .Set(x => x.EditableErrorRowsObjectKey, editableErrorRowsObjectKey)
                .Set(x => x.EditableErrorRowCount, editableErrorRowCount);

            return Repository.UpdateAsync(id, update, upsert: false);
        }

        public Task IncrementRetryAsync(string id)
        {
            return Repository.UpdateAsync(id, UpdateBuilder.Inc(x => x.RetryCount, 1), upsert: false);
        }
    }
}
