using System.Text.Json;

namespace EIMSNext.Json.Serialization
{
    /// <summary>
    /// 动态字段（FormData.Data 及其衍生结构）的「JSON → CLR 类型」唯一还原入口。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 历史上这套规则被抄了四份：持久化层 <c>DynamicJsonbReader</c>（DB 读回）、
    /// <see cref="DictionaryJsonConverter"/>（API 请求）、<see cref="ObjectJsonConverter"/>（object 声明成员）、
    /// 导入侧 <c>ImportCellConverters.UnwrapJsonValue</c>。四份的数值规则并不一致
    /// （有的一份没有 decimal、一份多一个 int 档），导致同一个 <c>12.5</c> 从 DB 读回是
    /// <see cref="decimal"/>、从导入写入是 <see cref="double"/>；而变更日志用 <c>Equals</c> 比较
    /// （<c>Equals(12.5m, 12.5d) == false</c>），于是每次保存都会产生一条虚假变更记录。
    /// 这里把规则收敛成一份，四条路径统一调用，类型契约由
    /// <c>Tests/EIMSNext.Async.Tests/DynamicValueParityTests.cs</c> 守门。
    /// </para>
    /// <para>
    /// 还原规则：对象→<c>Dictionary&lt;string, object?&gt;</c>、数组→<c>List&lt;object?&gt;</c>、
    /// 字符串→<see cref="string"/>、整数→<see cref="long"/>、小数→<see cref="decimal"/>、
    /// 溢出→<see cref="double"/>、true/false→<see cref="bool"/>、null→<c>null</c>。
    /// DateTime 经默认序列化是 ISO 字符串，读回为 string，由下游按字段语义解析。
    /// </para>
    /// <para>
    /// 两个入口（<see cref="Utf8JsonReader"/> 流式、<see cref="JsonElement"/> 文档式）必须产出完全一致的结果，
    /// 这是跨路径类型一致的前提。
    /// </para>
    /// </remarks>
    public static class DynamicValueReader
    {
        /// <summary>读取当前 token 所代表的值；reader 需已定位到该值的起始 token。</summary>
        public static object? ReadValue(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Number:
                    return ReadNumber(ref reader);
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.StartObject:
                    return ReadObject(ref reader);
                case JsonTokenType.StartArray:
                    return ReadArray(ref reader);
                default:
                    return null;
            }
        }

        /// <summary>读取一个对象；reader 需已定位到 <see cref="JsonTokenType.StartObject"/>，返回时已消费 EndObject。</summary>
        public static Dictionary<string, object?> ReadObject(ref Utf8JsonReader reader)
        {
            var dictionary = new Dictionary<string, object?>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return dictionary;
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException();

                var propertyName = reader.GetString();
                reader.Read();
                if (!string.IsNullOrEmpty(propertyName))
                    dictionary[propertyName] = ReadValue(ref reader);
            }

            throw new JsonException();
        }

        /// <summary>从 <see cref="JsonElement"/> 还原值（文档式入口，与流式入口等价）。</summary>
        public static object? FromJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    return ToDictionary(element);
                case JsonValueKind.Array:
                    return ToList(element);
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return NumberFromJsonElement(element);
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }

        /// <summary>从 <see cref="JsonElement"/> 还原对象（文档式入口）。</summary>
        /// <param name="element">处于 Object 值的 JSON 元素。</param>
        public static Dictionary<string, object?> ToDictionary(JsonElement element)
        {
            var dictionary = new Dictionary<string, object?>();
            foreach (var property in element.EnumerateObject())
            {
                dictionary[property.Name] = FromJsonElement(property.Value);
            }

            return dictionary;
        }

        /// <summary>把任意数值归一化为动态字段的统一表示：整数→long、小数→decimal、溢出→double。</summary>
        /// <param name="value">待归一化的值；非数值原样返回。</param>
        /// <remarks>
        /// 供非 JSON 来源的数值使用（Excel 导入的 <see cref="double"/> 单元格值、各宽度整型等）。
        /// 它们最终会落进 FormData.Data，必须与 JSON 还原路径的类型一致，否则变更日志会误判。
        /// </remarks>
        public static object? NormalizeNumber(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case long or decimal:
                    return value;
                case double doubleValue:
                    return NormalizeNumber(doubleValue);
                case float floatValue:
                    return NormalizeNumber((double)floatValue);
                case byte or sbyte or short or ushort or int or uint:
                    return Convert.ToInt64(value);
                default:
                    return value;
            }
        }

        /// <summary>把 <see cref="double"/> 归一化为 long / decimal / double。</summary>
        /// <returns>整数值→<see cref="long"/>；可表示的小数→<see cref="decimal"/>；超出范围→原值。</returns>
        public static object NormalizeNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return value;

            if (value >= long.MinValue && value <= long.MaxValue && Math.Floor(value) == value)
                return (long)value;

            try
            {
                return (decimal)value;
            }
            catch (OverflowException)
            {
                return value;
            }
        }

        private static List<object?> ReadArray(ref Utf8JsonReader reader)
        {
            var list = new List<object?>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                list.Add(ReadValue(ref reader));
            }

            return list;
        }

        private static List<object?> ToList(JsonElement element)
            => element.EnumerateArray().Select(FromJsonElement).ToList();

        private static object? ReadNumber(ref Utf8JsonReader reader)
        {
            if (reader.TryGetInt64(out var longValue))
                return longValue;
            if (reader.TryGetDecimal(out var decimalValue))
                return decimalValue;
            return reader.GetDouble();
        }

        private static object? NumberFromJsonElement(JsonElement element)
        {
            if (element.TryGetInt64(out var longValue))
                return longValue;
            if (element.TryGetDecimal(out var decimalValue))
                return decimalValue;
            return element.GetDouble();
        }
    }
}
