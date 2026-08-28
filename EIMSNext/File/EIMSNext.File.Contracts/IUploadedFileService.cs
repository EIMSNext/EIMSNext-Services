using EIMSNext.Core.Services;
using EIMSNext.Entities;

namespace EIMSNext.File.Contracts
{
    public sealed record UploadedFileUpload(Stream Content, string FileName, long FileSize);

    public interface IUploadedFileService : IService<UploadedFile>
    {
        IReadOnlyList<UploadedFile> Upload(IEnumerable<UploadedFileUpload> files, string corpId);
    }
}
