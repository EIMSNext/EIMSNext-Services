using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EIMSNext.Storage.Abstractions
{
    public abstract class StorageProviderBase<T> : IStorageProvider where T : IStorageProvider
    {
        protected StorageProviderBase(IOptions<StorageConfiguration> settings, ILogger<T> logger) : this(settings.Value, logger)
        {

        }
        protected StorageProviderBase(StorageConfiguration setting, ILogger<T> logger)
        {
            Setting = setting;
            Logger = logger;
        }

        protected ILogger<T> Logger { get; private set; }
        public StorageConfiguration Setting { get; private set; }

        public abstract bool Upload(byte[] content, string objKey);

        public abstract bool Upload(Stream content, string objKey);

        public abstract Stream? Download(string objKey);

        public abstract bool Exists(string objKey);
        public abstract void Delete(List<string> objKeys);

        public abstract IEnumerable<string> List(string prefix, DateTime? beginTime);

        #region Helper

        #endregion
    }
}
