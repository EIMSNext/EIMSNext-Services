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
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
            var expando = new ExpandoObject();
            if (dict != null)
            {
                expando = ConvertExpendo(dict);
            }
            return expando;
        }

        private ExpandoObject ConvertExpendo(IDictionary<string, object> dict)
        {
            var expando = new ExpandoObject();
            var target = (IDictionary<string, object?>)expando;
            foreach (var kvp in dict)
            {
                if (kvp.Value is JsonElement)
                {
                    target.Add(kvp.Key, ConvertJsonValue((JsonElement)kvp.Value));
                }
                else if (kvp.Value is IEnumerable)
                    target.Add(kvp.Key, ConvertExpendoList((IEnumerable)kvp.Value));
                else if (kvp.Value is IDictionary<string, object>)
                    target.Add(kvp.Key, ConvertExpendo((IDictionary<string, object>)kvp.Value));
                else
                    target.Add(kvp.Key, kvp.Value);
            }
            return expando;
        }
        private IList<object?> ConvertExpendoList(IEnumerable objList)
        {
            var list = new List<object?>();
            foreach (var x in objList)
            {
                if (x is JsonElement)
                {
                    list.Add(ConvertJsonValue((JsonElement)x));
                }
                else if (x is IEnumerable)
                    list.Add(ConvertExpendoList((IEnumerable)x));
                else if (x is IDictionary<string, object>)
                    list.Add(ConvertExpendo((IDictionary<string, object>)x));
                else
                    list.Add(x);
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
            JsonSerializer.Serialize(writer, value as IDictionary<string, object>, options);
        }
    }
}
