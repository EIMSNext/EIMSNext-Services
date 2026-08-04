using System.Dynamic;

namespace EIMSNext.Common.Extensions
{
    public static class ExpandoObjectExtension
    {
        public static bool ContainsKey(this ExpandoObject obj, string prop)
        {
            return (obj as IDictionary<string, object?>).ContainsKey(prop);
        }

        /// <summary>
        /// 从 <see cref="ExpandoObject"/> 中按键读取值。
        /// </summary>
        /// <param name="obj">动态对象。</param>
        /// <param name="key">键名。</param>
        /// <returns>对应的值；不存在时返回 <c>null</c>。</returns>
        public static object? AsDictionaryValue(this ExpandoObject obj, string key)
        {
            var dict = (IDictionary<string, object?>)obj;
            return dict.TryGetValue(key, out var value) ? value : null;
        }
    }
}
