using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Storage.Abstractions;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class UploadedFileService : EntityServiceBase<UploadedFile>, IUploadedFileService
    {
        private readonly IStorageProvider _storage;

        public UploadedFileService(IResolver resolver) : base(resolver)
        {
            _storage = resolver.Resolve<IStorageProvider>();
        }

        public IReadOnlyList<UploadedFile> Upload(IEnumerable<UploadedFileUpload> files, string corpId)
        {
            var inputs = files.ToList();
            if (inputs.Count == 0) return [];
            if (string.IsNullOrWhiteSpace(corpId)) throw new ArgumentException("企业 ID 不能为空", nameof(corpId));

            var attachments = new List<UploadedFile>(inputs.Count);
            var uploadedPaths = new List<string>(inputs.Count);
            try
            {
                foreach (var input in inputs)
                {
                    var fileName = Path.GetFileName(input.FileName);
                    var extension = Path.GetExtension(fileName);
                    var savePath = $"{_storage.Setting.UploadFolder}/{corpId}/{Guid.NewGuid():N}{extension}";
                    if (!_storage.Upload(input.Content, savePath))
                        throw new InvalidOperationException($"上传文件失败: {fileName}");

                    uploadedPaths.Add(savePath);
                    attachments.Add(new UploadedFile
                    {
                        CorpId = corpId,
                        FileName = fileName,
                        SavePath = savePath,
                        ThumbPath = $"{_storage.Setting.UploadFolder}/{corpId}/thumb/{Path.GetFileName(savePath)}",
                        FileExt = extension,
                        FileSize = input.FileSize,
                    });
                }

                Add(attachments);
                return attachments;
            }
            catch
            {
                _storage.Delete(uploadedPaths);
                throw;
            }
        }
    }
}
