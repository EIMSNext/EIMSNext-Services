using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EIMSNext.Common.Serialization
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
                if (kvp.Value is IList<object>)
                    (expando as IDictionary<string, object>).Add(kvp.Key, ConvertExpendoList((IList<object>)kvp.Value));
                else if (kvp.Value is IDictionary<string, object>)
                    (expando as IDictionary<string, object>).Add(kvp.Key, ConvertExpendo((IDictionary<string, object>)kvp.Value));
                else
                    (expando as IDictionary<string, object>).Add(kvp.Key, kvp.Value);
            }
            return expando;
        }
        private IList<object> ConvertExpendoList(IList<object> objList)
        {
            var list = new List<object>();
            objList.ForEach(x =>
             {
                 if (x is IList<object>)
                     list.Add(ConvertExpendoList((IList<object>)x));
                 else if (x is IDictionary<string, object>)
                     list.Add(ConvertExpendo((IDictionary<string, object>)x));
                 else
                     list.Add(x);
             });
            return list;
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
