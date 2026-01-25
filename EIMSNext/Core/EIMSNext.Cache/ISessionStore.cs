namespace EIMSNext.Cache
{
    public interface ISessionStore
    {
        IEnumerable<T> GetAll<T>(DataVersion version = DataVersion.Temp) where T : class;
        T? Get<T>(string key, DataVersion version = DataVersion.Temp, Func<string, T?>? getter = null) where T : class;
        void Set<T>(string key, T value, DataVersion version = DataVersion.Temp) where T : class;
        void Remove<T>(string key, DataVersion version = DataVersion.Temp) where T : class;
        bool Contains<T>(string key, DataVersion version = DataVersion.Temp) where T : class;
    }
    public enum DataVersion
    {
        Temp,
        Old,
        New
    }
}
