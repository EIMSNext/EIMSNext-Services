using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Async.Tasks.Export
{
public interface IExportProcessor
{
        Task<ExportFileBuilder.ExportFileResult> ExportAsync(
            ExportLog exportLog,
            IResolver resolver,
            CancellationToken ct);
    }
}
