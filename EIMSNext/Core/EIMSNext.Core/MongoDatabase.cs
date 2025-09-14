using EIMSNext.Core.Serialization;

using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

namespace EIMSNext.Core
{
    public static class MongoDatabase
    {
        public static void RegisterConventions()
        {
            // Mongo注册命名规范，因为它区分大小写，规范对OData有影响
            var camelCaseConvention = new ConventionPack {
                new IgnoreIfNullConvention(true),
                new IgnoreExtraElementsConvention(true),
                new CamelCaseElementNameConvention(),
             };
            ConventionRegistry.Register("CamelCase", camelCaseConvention, type => true);
        }

        public static void RegisterSerializers()
        {
            BsonSerializer.RegisterSerializer(new ObjectSerializer(ObjectSerializer.AllAllowedTypes));
            BsonSerializer.RegisterSerializer(new UnixMillisecondsDateTimeSerializer());

            BsonSerializer.RegisterIdGenerator(typeof(string), StringObjectIdGenerator.Instance);
            BsonSerializer.UseNullIdChecker = true;
        }
    }
}
