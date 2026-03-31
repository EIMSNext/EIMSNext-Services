using System.Collections;
using System.Collections.Generic;
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
            foreach (var kvp in dict)
            {
                if (kvp.Value is JsonElement)
                {
                    var el = (JsonElement)kvp.Value;
                    if (el.ValueKind == JsonValueKind.Array)
                        (expando as IDictionary<string, object>).Add(kvp.Key, ConvertJsonArray(el));
                    else if (el.ValueKind == JsonValueKind.Object)
                        (expando as IDictionary<string, object>).Add(kvp.Key, ConvertJsonObject(el));
                    else
                        (expando as IDictionary<string, object>).Add(kvp.Key, el);
                }
                else if (kvp.Value is IEnumerable)
                    (expando as IDictionary<string, object>).Add(kvp.Key, ConvertExpendoList((IEnumerable)kvp.Value));
                else if (kvp.Value is IDictionary<string, object>)
                    (expando as IDictionary<string, object>).Add(kvp.Key, ConvertExpendo((IDictionary<string, object>)kvp.Value));
                else
                    (expando as IDictionary<string, object>).Add(kvp.Key, kvp.Value);
            }
            return expando;
        }
        private IList<object> ConvertExpendoList(IEnumerable objList)
        {
            var list = new List<object>();
            foreach (var x in objList)
            {
                if (x is JsonElement)
                {
                    var el = (JsonElement)x;
                    if (el.ValueKind == JsonValueKind.Array)
                        list.Add(ConvertJsonArray(el));
                    else if (el.ValueKind != JsonValueKind.Object)
                        list.Add(ConvertJsonObject(el));
                    else
                        list.Add(el);
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
        private IList<object> ConvertJsonArray(JsonElement objList)
        {
            var list = new List<object>();
            foreach (var el in objList.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Array)
                    list.Add(ConvertJsonArray(el));
                else if (el.ValueKind != JsonValueKind.Object)
                    list.Add(ConvertJsonObject(el));
                else
                    list.Add(el);
            }
            return list;
        }
        private ExpandoObject ConvertJsonObject(JsonElement dict)
        {
            var expando = new ExpandoObject();
            foreach (var kvp in dict.EnumerateObject())
            {
                var key = kvp.Name.ToLower();
                var el = kvp.Value;

                if (el.ValueKind == JsonValueKind.Array)
                    (expando as IDictionary<string, object>).Add(key, ConvertJsonArray(el));
                else if (el.ValueKind != JsonValueKind.Object)
                    (expando as IDictionary<string, object>).Add(key, ConvertJsonObject(el));
                else
                    (expando as IDictionary<string, object>).Add(key, el);
            }
            return expando;
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
