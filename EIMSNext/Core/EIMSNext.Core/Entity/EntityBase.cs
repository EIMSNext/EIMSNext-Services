using System.Dynamic;
using EIMSNext.Common.Extension;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EIMSNext.Core.Entity
{
    public abstract class MongoEntityBase : IMongoEntity
    {
        [BsonId, BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;
    }
    public abstract class EntityBase : MongoEntityBase, IEntity
    {
        public Operator? CreateBy { get; set; }
        public long CreateTime { get; set; }
        public Operator? UpdateBy { get; set; }
        public long? UpdateTime { get; set; }

        public bool? DeleteFlag { get; set; }= false;
    }

    /// <summary>
    /// 企业级
    /// </summary>
    public abstract class CorpEntityBase : EntityBase, IEntity, ICorpOwned
    {
        /// <summary>
        /// 企业ID，设置为可空类型，方便序列化时忽略
        /// </summary>
        public string? CorpId { get; set; }
    }

    public abstract class DynamicEntity : CorpEntityBase, IEntity
    {
        public DynamicEntity()
        {
        }
        //测试用方法
        public DynamicEntity(string dataJson)
        {
            if (!string.IsNullOrEmpty(dataJson))
                Data = dataJson.DeserializeFromJson<ExpandoObject>()!;
        }

        public ExpandoObject Data { get; set; } = new ExpandoObject { };
    }
}
