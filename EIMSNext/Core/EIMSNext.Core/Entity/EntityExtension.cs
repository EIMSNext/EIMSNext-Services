using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace EIMSNext.Core.Entity
{
    public static class EntityExtension
    {
        public static BsonDocument ToBson<T>(this T entity, Action<T>? action = null) where T : IEntity
        {
            if (action != null) action(entity);

            return entity.ToBsonDocument();
        }

        public static IEnumerable<BsonDocument> ToBson<T>(this IEnumerable<T> entities, Action<T>? action = null) where T : IEntity
        {
            foreach (T entity in entities)
            {
                yield return entity.ToBson(action);
            }
        }

        public static T To<T>(this BsonDocument bson) where T : IEntity
        {
            return BsonSerializer.Deserialize<T>(bson);
        }

        public static IEnumerable<T> To<T>(this IEnumerable<BsonDocument> bsons) where T : IEntity
        {
            foreach (var bson in bsons)
            {
                yield return bson.To<T>();
            }
        }
    }
}
