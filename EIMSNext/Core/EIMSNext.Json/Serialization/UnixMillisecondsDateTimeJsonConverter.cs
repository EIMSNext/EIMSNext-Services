using System.Text.Json;
using System.Text.Json.Serialization;

namespace EIMSNext.Json.Serialization
{
    public class UnixMillisecondsDateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return ToDateTimeMs(reader.GetInt64());
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(ToTimeStampMs(value));
        }

        private  long ToTimeStampMs(DateTime dt)
        {
            return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
        }

        private DateTime ToDateTimeMs(long ms)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }
    }
}
