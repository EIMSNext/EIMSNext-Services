using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace EIMSNext.Core.Mongo.Serialization
{
    /// <summary>
    /// 将 <see cref="DateTime"/> 序列化为 Unix 毫秒时间戳的序列化器。
    /// </summary>
    public class UnixMillisecondsDateTimeSerializer : SerializerBase<DateTime>
    {
        /// <summary>
        /// 将 <see cref="DateTime"/> 序列化为 Unix 毫秒时间戳。
        /// </summary>
        /// <param name="context">序列化上下文。</param>
        /// <param name="args">序列化参数。</param>
        /// <param name="value">要序列化的值。</param>
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime value)
        {
            var writer = context.Writer;
            writer.WriteInt64(BsonUtils.ToMillisecondsSinceEpoch(value));
        }

        /// <summary>
        /// 将 Unix 毫秒时间戳反序列化为 <see cref="DateTime"/>。
        /// </summary>
        /// <param name="context">反序列化上下文。</param>
        /// <param name="args">反序列化参数。</param>
        /// <returns>反序列化后的时间。</returns>
        public override DateTime Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var reader = context.Reader;
            return BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(reader.ReadInt64());
        }
    }
}
