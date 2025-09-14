using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EIMSNext.Common.Serialization
{
    public class FlexibleEnumConverterFactory : JsonConverterFactory
    {
        private static Dictionary<Type, JsonConverter> enumConverters = new Dictionary<Type, JsonConverter>();
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        {
            if (!enumConverters.TryGetValue(type, out JsonConverter? converter))
            {
                // 动态生成特定枚举类型的转换器实例
                converter = Activator.CreateInstance(
                    typeof(FlexibleEnumConverter<>).MakeGenericType(type),
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    args: null,
                    culture: null
                ) as JsonConverter;
            }

            return converter!;
        }

        private class FlexibleEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
        {
            public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var enumType = typeof(TEnum);

                switch (reader.TokenType)
                {
                    case JsonTokenType.Number:
                        {
                            if (reader.TryGetInt32(out int numValue) && (Enum.IsDefined(enumType, numValue) || enumType.IsDefined(typeof(FlagsAttribute), false)))
                                return (TEnum)(object)numValue;
                        }
                        break;
                    case JsonTokenType.String:
                        {
                            var strValue = reader.GetString();
                            if (int.TryParse(strValue, out var numValue))
                            {
                                if (Enum.IsDefined(enumType, numValue) || enumType.IsDefined(typeof(FlagsAttribute), false))
                                    return (TEnum)(object)numValue;
                            }
                            if (Enum.TryParse(strValue, ignoreCase: true, out TEnum enValue))
                                return enValue;
                        }
                        break;
                }

                throw new JsonException($"无法将值转换为枚举类型 {typeof(TEnum)}");
            }

            public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
            {
                //序列化时统一输出为字符串（可选：可配置为数字）
                //输出为整型字符串与ODataEnumSerializer统一
                writer.WriteNumberValue(Convert.ToInt32(value));
            }
        }
    }
}
