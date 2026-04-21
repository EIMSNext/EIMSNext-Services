using EIMSNext.Common.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class ExportLogService(IResolver resolver) : EntityServiceBase<ExportLog>(resolver), IExportLogService
    {
        public async Task<ExportLog?> GetDuplicatedPendingAsync(string corpId, string createBy, string dedupKey)
        {
            return await Repository.Find(x =>
                    x.CorpId == corpId &&
                    x.CreateBy != null &&
                    x.CreateBy.Value == createBy &&
                    x.DedupKey == dedupKey &&
                    (x.Status == ExportLogStatus.Pending || x.Status == ExportLogStatus.Processing))
                .SortByDescending(x => x.CreateTime)
                .FirstOrDefaultAsync();
        }

        public Task MarkProcessingAsync(string id)
        {
            return Repository.UpdateAsync(
                id,
                UpdateBuilder.Set(x => x.Status, ExportLogStatus.Processing),
                upsert: false);
        }

        public Task MarkSucceededAsync(string id, string fileName, string downloadUrl, long totalCount, ExportFormat actualFormat)
        {
            var update = UpdateBuilder
                .Set(x => x.Status, ExportLogStatus.Succeeded)
                .Set(x => x.FileName, fileName)
                .Set(x => x.DownloadUrl, downloadUrl)
                .Set(x => x.TotalCount, totalCount)
                .Set(x => x.ActualFormat, actualFormat)
                .Set(x => x.FinishTime, DateTime.UtcNow.ToTimeStampMs())
                .Set(x => x.ErrorMessage, null as string);

            return Repository.UpdateAsync(id, update, upsert: false);
        }

        public Task MarkFailedAsync(string id, string errorMessage)
        {
            var update = UpdateBuilder
                .Set(x => x.Status, ExportLogStatus.Failed)
                .Set(x => x.ErrorMessage, errorMessage)
                .Set(x => x.FinishTime, DateTime.UtcNow.ToTimeStampMs());

            return Repository.UpdateAsync(id, update, upsert: false);
        }
    }
}
