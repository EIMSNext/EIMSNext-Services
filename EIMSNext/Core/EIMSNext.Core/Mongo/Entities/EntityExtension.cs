using MongoDB.Bson;
using MongoDB.Bson.Serialization;

using EIMSNext.Core.Abstractions;

namespace EIMSNext.Core.Mongo.Entities
{
    /// <summary>
    /// 实体与 Bson 文档之间的转换扩展方法。
    /// </summary>
    public static class EntityExtension
    {
        /// <summary>
        /// 将实体转换为 Bson 文档。
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IEntity"/> 的实体类型。</typeparam>
        /// <param name="entity">实体。</param>
        /// <param name="action">转换前对实体执行的操作。</param>
        /// <returns>转换后的 Bson 文档。</returns>
        public static BsonDocument ToBson<T>(this T entity, Action<T>? action = null) where T : IEntity
        {
            if (action != null) action(entity);

            return entity.ToBsonDocument();
        }

        /// <summary>
        /// 将实体集合转换为 Bson 文档集合。
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IEntity"/> 的实体类型。</typeparam>
        /// <param name="entities">实体集合。</param>
        /// <param name="action">转换前对每个实体执行的操作。</param>
        /// <returns>转换后的 Bson 文档集合。</returns>
        public static IEnumerable<BsonDocument> ToBson<T>(this IEnumerable<T> entities, Action<T>? action = null) where T : IEntity
        {
            foreach (T entity in entities)
            {
                yield return entity.ToBson(action);
            }
        }

        /// <summary>
        /// 将 Bson 文档反序列化为实体。
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IEntity"/> 的实体类型。</typeparam>
        /// <param name="bson">Bson 文档。</param>
        /// <returns>反序列化后的实体。</returns>
        public static T To<T>(this BsonDocument bson) where T : IEntity
        {
            return BsonSerializer.Deserialize<T>(bson);
        }

        /// <summary>
        /// 将 Bson 文档集合反序列化为实体集合。
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IEntity"/> 的实体类型。</typeparam>
        /// <param name="bsons">Bson 文档集合。</param>
        /// <returns>反序列化后的实体集合。</returns>
        public static IEnumerable<T> To<T>(this IEnumerable<BsonDocument> bsons) where T : IEntity
        {
            foreach (var bson in bsons)
            {
                yield return bson.To<T>();
            }
        }
    }
}
