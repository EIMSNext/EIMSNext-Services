using EIMSNext.Common.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
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
            var pending = FilterBuilder.Eq(x => x.Status, FormDataImportStatus.Pending);
            var expiredLease = FilterBuilder.Or(
                FilterBuilder.Lt(x => x.ProcessingExpireTime, now),
                FilterBuilder.Eq(x => x.ProcessingExpireTime, null));
            var expiredProcessing = FilterBuilder.And(
                FilterBuilder.Eq(x => x.Status, FormDataImportStatus.Processing),
                expiredLease);
            var filter = FilterBuilder.And(
                FilterBuilder.Eq(x => x.Id, id),
                FilterBuilder.Eq(x => x.RetryCount, retryCount),
                FilterBuilder.Or(pending, expiredProcessing));
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

        public Task UpdateEditableErrorsAsync(string id, string? editableErrorRowsJson, string? editableErrorRowsObjectKey, int editableErrorRowCount)
        {
            var update = UpdateBuilder
                .Set(x => x.EditableErrorRowsJson, editableErrorRowsJson)
                .Set(x => x.EditableErrorRowsObjectKey, editableErrorRowsObjectKey)
                .Set(x => x.EditableErrorRowCount, editableErrorRowCount);

            return Repository.UpdateAsync(id, update, upsert: false);
        }

        public Task PrepareRetryAsync(string id, string editableErrorRowsJson, int editableErrorRowCount)
        {
            var update = UpdateBuilder
                .Set(x => x.Status, FormDataImportStatus.Pending)
                .Set(x => x.TotalCount, editableErrorRowCount)
                .Set(x => x.ProcessedCount, 0)
                .Set(x => x.AddCount, 0)
                .Set(x => x.UpdateCount, 0)
                .Set(x => x.FailedCount, 0)
                .Set(x => x.EditableErrorRowsJson, editableErrorRowsJson)
                .Set(x => x.EditableErrorRowsObjectKey, (string?)null)
                .Set(x => x.EditableErrorRowCount, editableErrorRowCount)
                .Set(x => x.ErrorMessage, (string?)null)
                .Set(x => x.StartTime, (long?)null)
                .Set(x => x.FinishTime, (long?)null)
                .Set(x => x.ProcessingExpireTime, (long?)null)
                .Inc(x => x.RetryCount, 1);

            return Repository.UpdateAsync(id, update, upsert: false);
        }

        public Task<int?> TryPrepareRetryAsync(string id, int expectedRetryCount, string editableErrorRowsJson, int editableErrorRowCount)
        {
            var filter = FilterBuilder.And(
                FilterBuilder.Eq(x => x.Id, id),
                FilterBuilder.Eq(x => x.Status, FormDataImportStatus.CompletedWithErrors),
                FilterBuilder.Eq(x => x.RetryCount, expectedRetryCount));
            var update = UpdateBuilder
                .Set(x => x.Status, FormDataImportStatus.Pending)
                .Set(x => x.TotalCount, editableErrorRowCount)
                .Set(x => x.ProcessedCount, 0)
                .Set(x => x.AddCount, 0)
                .Set(x => x.UpdateCount, 0)
                .Set(x => x.FailedCount, 0)
                .Set(x => x.EditableErrorRowsJson, editableErrorRowsJson)
                .Set(x => x.EditableErrorRowsObjectKey, (string?)null)
                .Set(x => x.EditableErrorRowCount, editableErrorRowCount)
                .Set(x => x.ErrorMessage, (string?)null)
                .Set(x => x.StartTime, (long?)null)
                .Set(x => x.FinishTime, (long?)null)
                .Set(x => x.ProcessingExpireTime, (long?)null)
                .Inc(x => x.RetryCount, 1);

            var result = Repository.UpdateMany(filter, update, upsert: false);
            return Task.FromResult(result.ModifiedCount == 1 ? expectedRetryCount + 1 : (int?)null);
        }

        public Task IncrementRetryAsync(string id)
        {
            return Repository.UpdateAsync(id, UpdateBuilder.Inc(x => x.RetryCount, 1), upsert: false);
        }
    }
}
