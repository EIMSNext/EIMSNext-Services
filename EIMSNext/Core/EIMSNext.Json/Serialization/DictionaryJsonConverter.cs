using System.Text.Json;
using System.Text.Json.Serialization;

namespace EIMSNext.Json.Serialization
{
    /// <summary>
    /// <see cref="Dictionary{String,Object}"/>（动态表单字段容器，原 ExpandoObject）的深度还原转换器。
    /// </summary>
    /// <remarks>
    /// <para>
    /// STJ 默认把 <c>Dictionary&lt;string, object?&gt;</c> 的值反序列化成 <see cref="JsonElement"/>，
    /// 公式/脚本引擎拿到后数值运算、布尔判断都会失效。本转换器在请求反序列化路径上做
    /// 「JsonElement → CLR 类型」还原，规则统一委托给 <see cref="DynamicValueReader"/>：
    /// 与持久化层（DB 读回）、导入层共用同一套实现，避免同一个数值在不同入口落到不同 CLR 类型
    /// （历史上各写一份，DB 侧是 decimal、导入侧是 double，<c>Equals</c> 判不等 → 虚假变更日志）。
    /// </para>
    /// <para>
    /// 写路径无需特殊处理：Dictionary 本身是 STJ 原生支持的形状，值（均为 CLR 类型）按运行时类型多态序列化。
    /// </para>
    /// </remarks>
    public class DictionaryJsonConverter : JsonConverter<Dictionary<string, object?>>
    {
        public override Dictionary<string, object?> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return DynamicValueReader.ReadObject(ref reader);
        }

        public override void Write(
            Utf8JsonWriter writer,
            Dictionary<string, object?> value,
            JsonSerializerOptions options)
        {
            // 以 IDictionary 声明类型序列化，避免再次命中本转换器导致无限递归；
            // object 值按运行时类型多态序列化。
            JsonSerializer.Serialize(writer, (IDictionary<string, object?>)value, options);
        }
    }
}
