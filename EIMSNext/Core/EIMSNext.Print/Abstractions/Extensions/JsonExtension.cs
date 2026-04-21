using System.Collections;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Nodes;
using EIMSNext.Json.Serialization;
using Json.Path;

namespace EIMSNext.Print.Extensions
{
    public static class JsonExtension
    {
        private const string FieldReg = @"<field.*?.*?[^>]*?>.*?</field>";
        private const string FidReg = " fid=\\s*(\\x27|\\x22)([^\\x27\\x22]*)(\\x27|\\x22)";

        private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext(typeof(JsonExtension));
        public static List<JsonObject> ConvertToJsonObject(this IEnumerable<object> datas)
        {
            var result = new List<JsonObject>();
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExpandoObjectJsonConverter());

            foreach (var obj in datas)
            {
                var expandoObj = obj.SerializeToJson(options).DeserializeFromJson<ExpandoObject>(options)!;
                var lowerExpandoObj = ConvertKeysToLowerCase(expandoObj);

                var str = JsonSerializer.Serialize(lowerExpandoObj, options);
                Logger.Information("格式化后表单数据。Data={Data}", lowerExpandoObj);

                var jObj = JsonSerializer.Deserialize<JsonNode>(str);
                if (jObj != null)
                    result.Add(jObj.AsObject());
            }

            return result;
        }

        private static ExpandoObject ConvertKeysToLowerCase(ExpandoObject expando)
        {
            var result = new ExpandoObject();
            var resultDict = (IDictionary<string, object?>)result;

            foreach (var kvp in expando)
            {
                var lowerKey = kvp.Key.ToLower();
                var value = kvp.Value;

                if (value is ExpandoObject childExpando)
                {
                    resultDict[lowerKey] = ConvertKeysToLowerCase(childExpando);
                }
                else if (value is IEnumerable list)
                {
                    var newList = new List<object>();
                    foreach (var item in list)
                    {
                        if (item is ExpandoObject itemExpando)
                        {
                            newList.Add(ConvertKeysToLowerCase(itemExpando));
                        }
                        else
                        {
                            newList.Add(item);
                        }
                    }
                    resultDict[lowerKey] = newList;
                }               
                else
                {
                    resultDict[lowerKey] = value;
                }
            }

            return result;
        }

        public static string GetJsonValue(this object data, string jsonPath)
        {
            var result = data.GetJsonNode(jsonPath);
            Logger.Debug("GetJsonValueByPath. Path={JsonPath}, Result={Result}", jsonPath, result);
            if (result is JsonObject)
            {
                var jobj = result.AsObject();
                if (jobj.ContainsKey("label"))
                    result = result["label"];
            }

            return result == null ? string.Empty : result.ToString();
        }
        public static JsonArray GetJsonArray(this object data, string jsonPath)
        {
            JsonObject jObj;
            if (data is JsonObject) jObj = (JsonObject)data;
            else jObj = FromObject(data).AsObject();

            var result = jObj.SelectSingleNode(jsonPath);
            if (IsNull(result)) return new JsonArray();

            if (result is JsonArray) return (JsonArray)result;
            return new JsonArray() { result };
        }

        private static bool IsNull(JsonNode? node)
        {
            if (node == null) return true;
            if (node is JsonValue)
            {
                return node.AsValue().GetValue<object>() == null;
            }

            return false;
        }

        private static JsonNode? GetJsonNode(this object data, string jsonPath)
        {
            if (data is JsonObject jObj) { }
            else jObj = FromObject(data).AsObject();

            return jObj.SelectSingleNode(jsonPath);
        }

        internal static JsonNode FromObject(object obj)
        {
            return JsonNode.Parse(JsonSerializer.Serialize(obj))!;
        }
        private static JsonNode? SelectSingleNode(this JsonNode json, string jsonPath)
        {
            var path = JsonPath.Parse(jsonPath);
            return path.Evaluate(json)?.Matches?.FirstOrDefault()?.Value;
        }
    }
}
