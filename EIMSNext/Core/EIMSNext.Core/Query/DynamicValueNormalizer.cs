using System.Text.Json;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态值规范化器，将 JSON 元素等转换为对应的基础类型。
    /// </summary>
    public static class DynamicValueNormalizer
    {
        /// <summary>
        /// 规范化动态值。
        /// </summary>
        /// <param name="value">原始值。</param>
        /// <returns>规范化后的值。</returns>
        public static object? Normalize(object? value)
        {
            if (value is decimal decimalValue
                && decimal.Truncate(decimalValue) == decimalValue
                && decimalValue <= long.MaxValue
                && decimalValue >= long.MinValue)
            {
                return (long)decimalValue;
            }

            if (value is not JsonElement element)
            {
                return value;
            }

            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var integer)
                    ? integer
                    : element.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(item => Normalize(item))
                    .ToList(),
                JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(item => item.Name, item => Normalize(item.Value)),
                _ => element.ToString(),
            };
        }
    }
}
