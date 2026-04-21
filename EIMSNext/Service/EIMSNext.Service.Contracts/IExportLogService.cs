using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IExportLogService : IService<ExportLog>
    {
        Task<ExportLog?> GetDuplicatedPendingAsync(string corpId, string createBy, string dedupKey);

        Task MarkProcessingAsync(string id);

        Task MarkSucceededAsync(string id, string fileName, string downloadUrl, long totalCount, ExportFormat actualFormat);

        Task MarkFailedAsync(string id, string errorMessage);
    }
}
