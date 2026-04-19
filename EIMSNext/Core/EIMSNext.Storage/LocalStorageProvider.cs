using EIMSNext.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EIMSNext.Storage
{
    public class LocalStorageProvider : StorageProviderBase<LocalStorageProvider>
    {
        public LocalStorageProvider(IOptions<StorageConfiguration> settings, ILogger<LocalStorageProvider> logger) : base(settings, logger)
        {
        }

        public override bool Upload(byte[] content, string objKey)
        {
            if (string.IsNullOrEmpty(Setting.LocalPath) || string.IsNullOrEmpty(objKey))
                return false;

            using var stream = new MemoryStream(content, writable: false);
            return Upload(stream, objKey);

        }

        public override bool Upload(Stream content, string objKey)
        {
            if (string.IsNullOrEmpty(Setting.LocalPath) || string.IsNullOrEmpty(objKey))
                return false;

            var fullPath = Path.Combine(Setting.LocalPath, objKey.TrimStart('/'));
            var dirInfo = new FileInfo(fullPath);
            if (!dirInfo.Directory!.Exists)
                Directory.CreateDirectory(dirInfo.Directory.FullName);

            if (content.CanSeek)
            {
                content.Position = 0;
            }

            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            content.CopyTo(fileStream);

            return true;
        }

        public override Stream? Download(string objKey)
        {
            if (string.IsNullOrEmpty(Setting.LocalPath) || string.IsNullOrEmpty(objKey))
                return null;

            var fullPath = Path.Combine(Setting.LocalPath, objKey.TrimStart('/'));
            if (File.Exists(fullPath))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    return new MemoryStream(bytes);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"从LocalStorageProvider读取资源失败:{objKey}");
                }
            }

            return null;
        }

        public override bool Exists(string objKey)
        {
            if (string.IsNullOrEmpty(Setting.LocalPath) || string.IsNullOrEmpty(objKey))
                return false;

            var fullPath = Path.Combine(Setting.LocalPath, objKey.TrimStart('/'));
            return File.Exists(fullPath);
        }
        public override void Delete(List<string> objKeys)
        {
            if (!string.IsNullOrEmpty(Setting.LocalPath) && objKeys.Any())
            {
                objKeys.ForEach(objKey =>
                {
                    try
                    {
                        var fullPath = Path.Combine(Setting.LocalPath, objKey.TrimStart('/'));
                        File.Delete(fullPath);
                    }
                    catch { }
                });
            }
        }
        public override IEnumerable<string> List(string prefix, DateTime? beginTime)
        {
            throw new NotImplementedException();
        }
    }
}
