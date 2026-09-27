using System.Text.Json;
using System.Text.Json.Serialization;

using MongoDB.Bson;

namespace EIMSNext.Core.Mongo.Serialization
{
    /// <summary>
    /// <see cref="BsonDocument"/> 的 JSON 转换器。
    /// </summary>
    public class BsonDocumentJsonConverter : JsonConverter<BsonDocument>
    {
        /// <summary>
        /// 从 JSON 读取 <see cref="BsonDocument"/>。
        /// </summary>
        /// <param name="reader">JSON 读取器。</param>
        /// <param name="typeToConvert">目标类型。</param>
        /// <param name="options">序列化选项。</param>
        /// <returns>解析后的 Bson 文档。</returns>
        public override BsonDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var jsonDocument = JsonDocument.ParseValue(ref reader);
            return BsonDocument.Parse(jsonDocument.RootElement.GetRawText());
        }

        /// <summary>
        /// 将 <see cref="BsonDocument"/> 写入 JSON。
        /// </summary>
        /// <param name="writer">JSON 写入器。</param>
        /// <param name="value">Bson 文档。</param>
        /// <param name="options">序列化选项。</param>
        public override void Write(Utf8JsonWriter writer, BsonDocument value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(value.ToJson());
        }
    }
}
