namespace EIMSNext.Cache
{
    public interface ISessionStore
    {
        IEnumerable<T> GetAll<T>(DataVersion version = DataVersion.V0) where T : class;
        T? Get<T>(string key, DataVersion version = DataVersion.V0, Func<string, T?>? getter = null) where T : class;
        void Set<T>(string key, T value, DataVersion version = DataVersion.V0) where T : class;
        void Remove<T>(string key, DataVersion version = DataVersion.V0) where T : class;
        bool Contains<T>(string key, DataVersion version = DataVersion.V0) where T : class;
    }
    public enum DataVersion
    {
        V0,
        V1,
        V2
    }
}
