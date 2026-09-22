using System.Collections;
using System.Text.Json;
using EIMSNext.Json.Serialization;

namespace EIMSNext.Core.Extensions
{
    /// <summary>
    /// 动态字段容器（<c>Dictionary&lt;string, object?&gt;</c> / <c>IDictionary&lt;string, object?&gt;</c>）的扩展方法。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 历史上这些方法散落在 <c>EIMSNext.Flow.Core.CollectionExtensions</c> 与各消费方内部
    /// （<c>AsDictionary</c> 一度有 5 份私有实现、<c>ToScriptData</c> 2 份、子表单行归一化 2 份）。
    /// 它们与工作流、导出、导入都没有关系，属于「动态数据容器」这一核心概念的通用操作，
    /// 因此收在 Core 层：Flow.Core / Service / Async 三层都能引用，不必各抄一份。
    /// </para>
    /// <para>
    /// 容器统一按 <c>IDictionary&lt;string, object?&gt;</c> 接收：工作流上下文的 ExpandoObject
    /// 也实现该接口，读写两侧不必关心实际容器类型。
    /// </para>
    /// </remarks>
    public static class DynamicDataExtensions
    {
        /// <summary>存在则更新、不存在则新增。</summary>
        /// <param name="data">动态字典。</param>
        /// <param name="key">键。</param>
        /// <param name="value">值。</param>
        public static void AddOrUpdate(this IDictionary<string, object?> data, string key, object? value)
        {
            if (data.ContainsKey(key))
                data[key] = value;
            else
                data.Add(key, value);
        }

        /// <summary>按键读取值，不存在返回 <c>null</c>。</summary>
        /// <param name="data">动态字典。</param>
        /// <param name="key">键。</param>
        /// <returns>值或 <c>null</c>。</returns>
        public static object? GetValueOrDefault(this IDictionary<string, object?> data, string key)
        {
            return data.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>按键读取值并强转为 <typeparamref name="T"/>，不存在返回默认值。</summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="data">动态字典。</param>
        /// <param name="key">键。</param>
        /// <returns>值或默认值。</returns>
        /// <remarks>保持原有的强转语义：类型不匹配会抛 <see cref="InvalidCastException"/>，便于尽早暴露契约问题。</remarks>
        public static T? GetValueOrDefault<T>(this IDictionary<string, object?> data, string key)
        {
            return data.TryGetValue(key, out var value) ? (T?)value : default;
        }

        /// <summary>按键读取值并强转为 <typeparamref name="T"/>，取不到时返回 <paramref name="defaultValue"/>。</summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="data">动态字典。</param>
        /// <param name="key">键。</param>
        /// <param name="defaultValue">兜底值。</param>
        /// <returns>值或兜底值。</returns>
        /// <remarks>保持原有的强转语义，与 <see cref="GetValueOrDefault{T}"/> 一致。</remarks>
        public static T GetValue<T>(this IDictionary<string, object?> data, string key, T defaultValue)
        {
            return data.TryGetValue(key, out var value) ? (T?)value ?? defaultValue : defaultValue;
        }

        /// <summary>
        /// 读取子表单行集合，统一归一化为 <see cref="List{Dictionary}"/>。
        /// </summary>
        /// <param name="data">动态字典。</param>
        /// <param name="field">子表单字段名。</param>
        /// <returns>行集合；字段缺失或为空时返回空列表。</returns>
        /// <remarks>
        /// 兼容内存构造的 <c>List&lt;Dictionary&lt;string, object?&gt;&gt;</c>、
        /// jsonb / JSON 读回的 <c>List&lt;object?&gt;</c>、尚未还原的 <see cref="JsonElement"/> 等来源。
        /// 本方法是纯读：不做任何写回，需要把归一化结果放回容器时由调用方显式赋值
        /// （避免在只读路径上悄悄污染被跟踪实体的状态）。
        /// </remarks>
        public static List<Dictionary<string, object?>> GetRows(this IDictionary<string, object?> data, string field)
        {
            var rows = new List<Dictionary<string, object?>>();
            if (!data.TryGetValue(field, out var raw) || raw is null)
                return rows;

            if (raw is List<Dictionary<string, object?>> typed)
            {
                rows.AddRange(typed);
                return rows;
            }

            if (raw is JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.Array)
                    return rows;

                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                        rows.Add(DynamicValueReader.ToDictionary(item));
                }

                return rows;
            }

            if (raw is IEnumerable sequence and not string)
            {
                foreach (var item in sequence)
                {
                    if (item is JsonElement jsonItem)
                    {
                        if (jsonItem.ValueKind == JsonValueKind.Object)
                            rows.Add(DynamicValueReader.ToDictionary(jsonItem));
                    }
                    else if (item is IDictionary<string, object?> dictionary)
                    {
                        rows.Add(dictionary as Dictionary<string, object?> ?? new Dictionary<string, object?>(dictionary));
                    }
                }
            }

            return rows;
        }

        /// <summary>
        /// 把动态容器里的任意值当作「行/字典」看待，能还原成字典则返回字典，否则返回 <c>null</c>。
        /// </summary>
        /// <param name="value">待转换的值。</param>
        /// <returns>字典或 <c>null</c>。</returns>
        /// <remarks>
        /// 覆盖三种来源：CLR 字典（<c>Dictionary</c> / <c>ExpandoObject</c> 等 <c>IDictionary</c> 实现）、
        /// 非泛型字典、以及尚未还原的 <see cref="JsonElement"/> 对象。
        /// </remarks>
        public static IDictionary<string, object?>? AsDictionary(this object? value)
        {
            if (value is JsonElement element)
                return element.ValueKind == JsonValueKind.Object ? DynamicValueReader.ToDictionary(element) : null;

            if (value is IDictionary<string, object?> typed)
                return typed;

            if (value is IDictionary<string, object> objectDict)
                return objectDict.ToDictionary(x => x.Key, x => (object?)x.Value);

            if (value is IDictionary untyped)
            {
                var result = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in untyped)
                {
                    if (entry.Key is string key)
                        result[key] = entry.Value;
                }

                return result;
            }

            return null;
        }

        /// <summary>把动态字典转成脚本引擎的入参（剔除 null 值）。</summary>
        /// <param name="data">动态字典。</param>
        /// <returns>脚本入参字典。</returns>
        public static Dictionary<string, object> ToScriptData(this IDictionary<string, object?> data)
        {
            var scriptData = new Dictionary<string, object>();
            foreach (var kvp in data)
            {
                if (kvp.Value != null) scriptData.Add(kvp.Key, kvp.Value);
            }

            return scriptData;
        }
    }
}
