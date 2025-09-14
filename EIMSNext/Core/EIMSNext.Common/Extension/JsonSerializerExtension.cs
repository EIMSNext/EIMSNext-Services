using System.Text.Json;

namespace EIMSNext.Common.Extension
{
    public static class JsonSerializerExtension
    {
        private static JsonSerializerOptions? _options;

        public static void SetOptions(JsonSerializerOptions options)
        {
            _options = options;
        }

        public static string SerializeToJson(this object value)
        {
            return JsonSerializer.Serialize(value, _options);
        }

        public static T? DeserializeFromJson<T>(this string json)
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }
    }
}
