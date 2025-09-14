namespace EIMSNext.Cache
{
    public class SessionStore : ISessionStore
    {
        protected Dictionary<Type, Dictionary<string, object>> _store = new Dictionary<Type, Dictionary<string, object>>();

        public T? Get<T>(string key, DataVersion version = DataVersion.V0, Func<string, T?>? getter = null) where T : class
        {
            if (!_store.TryGetValue(typeof(T), out Dictionary<string, object>? dic))
            {
                dic = new Dictionary<string, object>();
                _store.Add(typeof(T), dic);
            }

            var dataKey = $"{key}_{version}";
            if (dic.TryGetValue(dataKey, out object? val))
            {
                return val as T;
            }
            else
            {
                if (getter != null)
                {
                    T? value = getter(key);
                    if (value != null)
                    {
                        dic.Add(dataKey, value);
                        return value;
                    }
                }
            }

            return null;
        }

        public void Remove<T>(string key, DataVersion version = DataVersion.V0) where T : class
        {
            if (_store.TryGetValue(typeof(T), out Dictionary<string, object>? dic))
            {
                var dataKey = $"{key}_{version}";
                if (dic.TryGetValue(dataKey, out object? val))
                {
                    dic.Remove(dataKey);
                }
            }
        }

        public void Set<T>(string key, T value, DataVersion version = DataVersion.V0) where T : class
        {
            if (!_store.TryGetValue(typeof(T), out Dictionary<string, object>? dic))
            {
                dic = new Dictionary<string, object>();
                _store.Add(typeof(T), dic);
            }

            var dataKey = $"{key}_{version}";
            if (dic.TryGetValue(dataKey, out object? val))
            {
                dic[dataKey] = val;
            }
            else
            {
                dic.Add(dataKey, value);
            }
        }
        public bool Contains<T>(string key, DataVersion version = DataVersion.V0) where T : class
        {
            if (_store.TryGetValue(typeof(T), out Dictionary<string, object>? dic))
            {
                var dataKey = $"{key}_{version}";
                return dic.ContainsKey(dataKey);               
            }

            return false;
        }
    }
}
