using EIMSNext.Common.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;

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
                .OrderByDescending(x => x.CreateTime)
                .FirstOrDefaultAsync();
        }

        public Task MarkProcessingAsync(string id)
        {
            return Repository.UpdateAsync(
                id,
                setters => setters.SetProperty(x => x.Status, ExportLogStatus.Processing));
        }

        public Task MarkSucceededAsync(string id, string fileName, string downloadUrl, long totalCount, ExportFormat actualFormat)
        {
            var finishTime = DateTime.UtcNow.ToTimeStampMs();
            return Repository.UpdateAsync(
                id,
                setters => setters
                    .SetProperty(x => x.Status, ExportLogStatus.Succeeded)
                    .SetProperty(x => x.FileName, fileName)
                    .SetProperty(x => x.DownloadUrl, downloadUrl)
                    .SetProperty(x => x.TotalCount, totalCount)
                    .SetProperty(x => x.ActualFormat, actualFormat)
                    .SetProperty(x => x.FinishTime, finishTime)
                    .SetProperty(x => x.ErrorMessage, null as string));
        }

        public Task MarkFailedAsync(string id, string errorMessage)
        {
            var finishTime = DateTime.UtcNow.ToTimeStampMs();
            return Repository.UpdateAsync(
                id,
                setters => setters
                    .SetProperty(x => x.Status, ExportLogStatus.Failed)
                    .SetProperty(x => x.ErrorMessage, errorMessage)
                    .SetProperty(x => x.FinishTime, finishTime));
        }
    }
}
