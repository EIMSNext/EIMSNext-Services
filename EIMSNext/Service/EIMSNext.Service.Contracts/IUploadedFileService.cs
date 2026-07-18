using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    public sealed record UploadedFileUpload(Stream Content, string FileName, long FileSize);

    public interface IUploadedFileService : IService<UploadedFile>
    {
        IReadOnlyList<UploadedFile> Upload(IEnumerable<UploadedFileUpload> files, string corpId);
    }
}
