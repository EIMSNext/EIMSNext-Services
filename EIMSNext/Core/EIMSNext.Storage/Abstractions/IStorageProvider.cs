namespace EIMSNext.Storage.Abstractions
{
    public interface IStorageProvider
    {
        StorageConfiguration Setting { get; }
        bool Upload(byte[] content, string objKey);
        Stream? Download(string objKey);
        bool Exists(string objKey);
        void Delete(List<string> objKeys);

        IEnumerable<string> List(string prefix, DateTime? beginTime);
    }
}
