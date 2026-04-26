using System.Collections;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EIMSNext.Json.Serialization
{
    public class ExpandoObjectJsonConverter : JsonConverter<ExpandoObject>
    {
        public override ExpandoObject Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(ref reader, options);
            var expando = new ExpandoObject();
            if (dict != null)
            {
                expando = ConvertExpendo(dict);
            }
            return expando;
        }

        private ExpandoObject ConvertExpendo(IDictionary<string, object?> dict)
        {
            var expando = new ExpandoObject();
            var target = (IDictionary<string, object?>)expando;
            foreach (var kvp in dict)
            {
                target.Add(kvp.Key, ConvertObjectValue(kvp.Value));
            }
            return expando;
        }

        private IList<object?> ConvertExpendoList(IEnumerable objList)
        {
            var list = new List<object?>();
            foreach (var x in objList)
            {
                list.Add(ConvertObjectValue(x));
            }
            return list;
        }

        private IList<object?> ConvertJsonArray(JsonElement objList)
        {
            var list = new List<object?>();
            foreach (var el in objList.EnumerateArray())
            {
                list.Add(ConvertJsonValue(el));
            }
            return list;
        }
        private ExpandoObject ConvertJsonObject(JsonElement dict)
        {
            var expando = new ExpandoObject();
            var target = (IDictionary<string, object?>)expando;
            foreach (var kvp in dict.EnumerateObject())
            {
                target.Add(kvp.Name, ConvertJsonValue(kvp.Value));
            }
            return expando;
        }

        private object? ConvertObjectValue(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is JsonElement element)
            {
                return ConvertJsonValue(element);
            }

            if (value is string)
            {
                return value;
            }

            if (value is int intValue)
            {
                return (long)intValue;
            }

            if (value is uint uintValue)
            {
                return (long)uintValue;
            }

            if (value is short shortValue)
            {
                return (long)shortValue;
            }

            if (value is ushort ushortValue)
            {
                return (long)ushortValue;
            }

            if (value is byte byteValue)
            {
                return (long)byteValue;
            }

            if (value is sbyte sbyteValue)
            {
                return (long)sbyteValue;
            }

            if (value is IDictionary<string, object?> dict)
            {
                return ConvertExpendo(dict);
            }

            if (value is IEnumerable list)
            {
                return ConvertExpendoList(list);
            }

            return value;
        }

        private object? ConvertJsonValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ConvertJsonObject(element),
                JsonValueKind.Array => ConvertJsonArray(element),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var longValue)
                    ? longValue
                    : element.TryGetDecimal(out var decimalValue)
                        ? decimalValue
                        : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.GetRawText(),
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            ExpandoObject value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value as IDictionary<string, object?>, options);
        }
    }
}
