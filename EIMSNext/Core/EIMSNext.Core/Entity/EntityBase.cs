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
        private DateTime _createTime;
        private DateTime? _updateTime;
        public Operator? CreateBy { get; set; }
        public DateTime CreateTime { get { return _createTime; } set { _createTime = value.ToUniversalTime(); } }
        public Operator? UpdateBy { get; set; }
        public DateTime? UpdateTime { get { return _updateTime; } set { _updateTime = value?.ToUniversalTime(); } }

        public bool? DeleteFlag { get; set; }
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
