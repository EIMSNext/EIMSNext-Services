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
            options.Converters.Add(new DictionaryJsonConverter());

            foreach (var obj in datas)
            {
                var dict = obj.SerializeToJson(options).DeserializeFromJson<Dictionary<string, object?>>(options)!;
                var lowerDict = ConvertKeysToLowerCase(dict);

                Logger.Information("格式化后表单数据。Data={Data}", lowerDict);

                var str = JsonSerializer.Serialize(lowerDict, options);
                var jObj = JsonSerializer.Deserialize<JsonNode>(str);
                if (jObj != null)
                    result.Add(jObj.AsObject());
            }

            return result;
        }

        private static Dictionary<string, object?> ConvertKeysToLowerCase(Dictionary<string, object?> dict)
        {
            var result = new Dictionary<string, object?>();

            foreach (var kvp in dict)
            {
                var lowerKey = kvp.Key.ToLower();
                var value = kvp.Value;

                if (value is Dictionary<string, object?> childDict)
                {
                    result[lowerKey] = ConvertKeysToLowerCase(childDict);
                }
                else if (value is string)
                {
                    result[lowerKey] = value;
                }
                else if (value is IEnumerable list)
                {
                    var newList = new List<object?>();
                    foreach (var item in list)
                    {
                        if (item is Dictionary<string, object?> itemDict)
                        {
                            newList.Add(ConvertKeysToLowerCase(itemDict));
                        }
                        else
                        {
                            newList.Add(item);
                        }
                    }
                    result[lowerKey] = newList;
                }
                else
                {
                    result[lowerKey] = value;
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
