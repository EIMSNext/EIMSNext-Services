using System.Text.Json.Nodes;

namespace System.Text.Json
{
    public static class JsonSerializerExtension
    {
        private static JsonSerializerOptions? _options;

        public static void SetOptions(JsonSerializerOptions options)
        {
            _options = options;
        }

        public static string SerializeToJson(this object value, JsonSerializerOptions? options = null)
        {
            return JsonSerializer.Serialize(value, options ?? _options);
        }

        public static T? DeserializeFromJson<T>(this string json, JsonSerializerOptions? options = null)
        {
            return JsonSerializer.Deserialize<T>(json, options ?? _options);
        }

        public static object? DeserializeFromJson(this string json, Type returnType, JsonSerializerOptions? options = null)
        {
            return JsonSerializer.Deserialize(json, returnType, options ?? _options);
        }

        public static T? DeserializeFromJson<T>(this JsonNode json, JsonSerializerOptions? options = null)
        {
            return json.Deserialize<T>(options ?? _options);
        }

        public static object? DeserializeFromJson(this JsonNode json, Type returnType, JsonSerializerOptions? options = null)
        {
            return json.Deserialize(returnType, options ?? _options);
        }
    }
}
