using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace EIMSNext.Core.Serialization
{
    public class UnixMillisecondsDateTimeSerializer : SerializerBase<DateTime>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime value)
        {
            var writer = context.Writer;
            writer.WriteInt64(BsonUtils.ToMillisecondsSinceEpoch(value));
        }
        public override DateTime Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var reader = context.Reader;
            return BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(reader.ReadInt64());
        }
    }
}
