using System.Collections;
using System.Dynamic;
using System.Text.Json;

namespace EIMSNext.Core.Repositories
{
    /// <summary>
    /// PostgreSQL 下 jsonb 读回来可能是 <see cref="JsonElement"/>、
    /// <see cref="ExpandoObject"/> 或普通 CLR 标量，这里统一按路径取到目标值。
    /// </summary>
    public static class DynamicPathValueReader
    {
        /// <summary>
        /// 按路径从对象上取值。
        /// </summary>
        /// <returns>取到的值；路径不存在时为 null。</returns>
        public static object? Read(object? root, IReadOnlyList<string> path)
        {
            var current = root;
            foreach (var segment in path)
            {
                current = Step(current, segment);
                if (current is null) return null;
            }

            return current;
        }

        /// <summary>
        /// 展开值为扁平序列：数组展开为多元素，其它值原样返回单元素。
        /// </summary>
        public static IEnumerable<object?> Flatten(object? value)
        {
            switch (value)
            {
                case null:
                    yield break;

                case string:
                    yield return value;
                    yield break;

                case JsonElement json when json.ValueKind == JsonValueKind.Array:
                    foreach (var item in json.EnumerateArray())
                    {
                        foreach (var nested in Flatten(JsonToClr(item)))
                            yield return nested;
                    }

                    yield break;

                case JsonElement json when json.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined:
                    yield break;

                case JsonElement json:
                    yield return JsonToClr(json);
                    yield break;

                case IDictionary<string, object?>:
                    yield return value;
                    yield break;

                case IEnumerable sequence:
                    foreach (var item in sequence)
                    {
                        foreach (var nested in Flatten(item))
                            yield return nested;
                    }

                    yield break;

                default:
                    yield return value;
                    yield break;
            }
        }

        /// <summary>
        /// 生成用于去重的稳定键。
        /// </summary>
        public static string ToDedupKey(object? value)
        {
            return value switch
            {
                null => string.Empty,
                string text => text,
                JsonElement json => json.ToString(),
                IDictionary<string, object?> dictionary =>
                    dictionary.TryGetValue("id", out var id) && id is not null
                        ? id.ToString() ?? string.Empty
                        : JsonSerializer.Serialize(dictionary),
                _ => value.ToString() ?? string.Empty,
            };
        }

        /// <summary>
        /// 递归降级为 CLR 对象，便于后续统一处理。
        /// </summary>
        private static object? JsonToClr(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in element.EnumerateObject())
                    {
                        dictionary[property.Name] = JsonToClr(property.Value);
                    }

                    return dictionary;
                }

                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(JsonToClr).ToList();

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    return element.TryGetInt64(out var longValue) ? longValue : element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                default:
                    return null;
            }
        }

        /// <summary>
        /// 沿单段路径取值。
        /// </summary>
        private static object? Step(object? current, string segment)
        {
            switch (current)
            {
                case null:
                    return null;

                case JsonElement json:
                    if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(segment, out var property))
                    {
                        return JsonToClr(property);
                    }

                    if (json.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var jsonIndex))
                    {
                        var array = json.EnumerateArray().ToList();
                        return jsonIndex >= 0 && jsonIndex < array.Count ? JsonToClr(array[jsonIndex]) : null;
                    }

                    return null;

                case IDictionary<string, object?> dictionary:
                    return dictionary.TryGetValue(segment, out var dictionaryValue) ? dictionaryValue : null;

                case IDictionary nonGeneric:
                    return nonGeneric.Contains(segment) ? nonGeneric[segment] : null;
            }

            if (int.TryParse(segment, out var index) && current is IList list)
            {
                return index >= 0 && index < list.Count ? list[index] : null;
            }

            // 普通 CLR 对象：按属性名反射取值。
            var propertyInfo = current.GetType().GetProperty(
                segment,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.IgnoreCase);
            return propertyInfo?.GetValue(current);
        }
    }
}
