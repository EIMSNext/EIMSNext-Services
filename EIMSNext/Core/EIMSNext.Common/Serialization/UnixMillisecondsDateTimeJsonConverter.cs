using System.Text.Json;
using System.Text.Json.Serialization;
using EIMSNext.Common.Extension;

namespace EIMSNext.Common.Serialization
{
    public class UnixMillisecondsDateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetInt64().ToDateTimeMs();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.ToTimeStampMs());
        }
    }
}
