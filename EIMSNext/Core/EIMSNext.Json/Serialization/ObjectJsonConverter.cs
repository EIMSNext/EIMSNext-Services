using System.Text.Json;
using System.Text.Json.Serialization;

namespace EIMSNext.Json.Serialization
{
    /// <summary>
    /// <see cref="object"/> 声明成员的深度还原转换器。
    /// </summary>
    /// <remarks>
    /// 还原规则统一委托给 <see cref="DynamicValueReader"/>，与 <see cref="DictionaryJsonConverter"/>、
    /// 持久化层、导入层保持一致（整数→long、小数→decimal、嵌套对象→Dictionary、数组→List）。
    /// 此前这里单独实现了「int 优先、无 decimal」的一套规则，与其余路径不一致。
    /// </remarks>
    public class ObjectJsonConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DynamicValueReader.ReadValue(ref reader);
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
