using EIMSNext.Core.Mongo.Serialization;

using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

namespace EIMSNext.Core.Mongo
{
    /// <summary>
    /// Mongo 数据库初始化工具，负责注册命名规范与序列化器。
    /// </summary>
    public static class MongoDatabase
    {
        /// <summary>
        /// 注册 Mongo 命名规范。
        /// </summary>
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

        /// <summary>
        /// 注册 Mongo 序列化器与 ID 生成器。
        /// </summary>
        public static void RegisterSerializers()
        {
            BsonSerializer.RegisterSerializer(new ObjectSerializer(ObjectSerializer.AllAllowedTypes));
            BsonSerializer.RegisterSerializer(new UnixMillisecondsDateTimeSerializer());

            BsonSerializer.RegisterIdGenerator(typeof(string), StringObjectIdGenerator.Instance);
            BsonSerializer.UseNullIdChecker = true;
        }
    }
}
