using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.Xml;
using EIMSNext.Entity;

namespace EIMSNext.Flow.Core
{
    public static class CollectionExtensions
    {
        public static void AddOrUpdate(this ExpandoObject expDo, string key, object? value)
        {
            var dic = (IDictionary<string, object?>)expDo;
            if (dic.ContainsKey(key))
                dic[key] = value;
            else
                dic.Add(key, value);
        }
        public static object? GetValueOrDefault(this ExpandoObject expDo, string key)
        {
            var dic = (IDictionary<string, object?>)expDo;
            if (dic.ContainsKey(key))
                return dic[key];

            return null;
        }
        public static T? GetValueOrDefault<T>(this ExpandoObject expDo, string key)
        {
            T? result = default(T);
            var dic = (IDictionary<string, object?>)expDo;
            if (dic.ContainsKey(key))
                result = (T?)dic[key];

            return result;
        }

        public static T GetValue<T>(this ExpandoObject expDo, string key, T defaultValue)
        {
            T? result = default(T);
            var dic = (IDictionary<string, object?>)expDo;
            if (dic.ContainsKey(key))
                result = (T?)dic[key];

            return result ?? defaultValue;
        }

        public static Dictionary<string, object> ToScriptData(this FormData formData)
        {
            var wrapData = new ExpandoObject();

            var pData = formData.Data;
            pData.TryAdd("createBy", formData.CreateBy);

            wrapData.TryAdd($"f_{formData.FormId}", pData);

            return new Dictionary<string, object>() { ["data"] = wrapData };
        }

        public static Dictionary<string, object> ToScriptData(this ExpandoObject expObj)
        {
            var scriptData = new Dictionary<string, object>();
            foreach (var kvp in expObj)
            {
                if (kvp.Value != null) scriptData.Add(kvp.Key, kvp.Value);
            }
            return scriptData;
        }
    }
}
