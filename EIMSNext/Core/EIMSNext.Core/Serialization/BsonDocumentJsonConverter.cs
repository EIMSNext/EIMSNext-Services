using System.Text.Json;
using System.Text.Json.Serialization;

using MongoDB.Bson;

namespace EIMSNext.Core.Serialization
{
    public class BsonDocumentJsonConverter : JsonConverter<BsonDocument>
    {
        public override BsonDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var jsonDocument = JsonDocument.ParseValue(ref reader);
            return BsonDocument.Parse(jsonDocument.RootElement.GetRawText());
        }

        public override void Write(Utf8JsonWriter writer, BsonDocument value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(value.ToJson());
        }
    }
}
