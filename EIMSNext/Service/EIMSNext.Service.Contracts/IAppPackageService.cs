using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IAppPackageService
    {
        Task<AppPackageExport> ExportAsync(string appProfileId);

        Task<AppPackagePreview> PreviewAsync(Stream stream, long fileLength);

        Task<AppPackageImportResult> ImportAsync(Stream stream, long fileLength);
    }
}
