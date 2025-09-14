using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace EIMSNext.Common.Serialization
{
    public class ObjectJsonConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Number:
                    if (reader.TryGetInt32(out int intValue))
                        return intValue;
                    if (reader.TryGetInt64(out long longValue))
                        return longValue;
                    return reader.GetDouble();
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.StartObject:
                    // 递归处理嵌套对象，转换为 Dictionary<string, object>
                    var dict = new Dictionary<string, object?>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                            return dict;
                        if (reader.TokenType != JsonTokenType.PropertyName)
                            throw new JsonException();
                        string? propertyName = reader.GetString();
                        reader.Read();
                        if(!string.IsNullOrEmpty(propertyName))
                        dict[propertyName] = Read(ref reader, typeof(object), options);
                    }
                    throw new JsonException();
                case JsonTokenType.StartArray:
                    // 递归处理数组，转换为 List<object>
                    var list = new List<object?>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        list.Add(Read(ref reader, typeof(object), options));
                    }
                    return list;
                default:
                    // 处理 null 或其他未覆盖的类型
                    return JsonDocument.ParseValue(ref reader).RootElement.Clone();
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
