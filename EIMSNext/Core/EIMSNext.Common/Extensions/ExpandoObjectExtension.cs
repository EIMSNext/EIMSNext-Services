namespace EIMSNext.Common.Extensions
{
    /// <summary>
    /// 动态字典（原 ExpandoObject，现统一为 Dictionary&lt;string, object?&gt;）的扩展方法。
    /// </summary>
    public static class ExpandoObjectExtension
    {
        /// <summary>
        /// 从动态字典中按键读取值。
        /// </summary>
        /// <param name="obj">动态字典（ExpandoObject / Dictionary 均可）。</param>
        /// <param name="key">键名。</param>
        /// <returns>对应的值；不存在时返回 <c>null</c>。</returns>
        public static object? AsDictionaryValue(this IDictionary<string, object?> obj, string key)
        {
            return obj.TryGetValue(key, out var value) ? value : null;
        }
    }
}
