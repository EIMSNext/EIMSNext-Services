using System.Text.Json;

namespace EIMSNext.Core.Query
{
    public static class DynamicValueNormalizer
    {
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
