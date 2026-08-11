using System.Dynamic;
using System.Text.Json;

using EIMSNext.Core.Abstractions;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EIMSNext.Core.Mongo.Entities
{
    /// <summary>Mongo 持久化实体公共字段。</summary>
    public abstract class MongoEntityBase : IMongoEntity
    {
        /// <summary>资源标识。</summary>
        [BsonId, BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;
    }
    /// <summary>包含审计字段的实体基类。</summary>
    public abstract class EntityBase : MongoEntityBase, IEntity
    {
        /// <summary>创建人。</summary>
        public Operator? CreateBy { get; set; }
        /// <summary>创建时间（Unix 毫秒）。</summary>
        public long CreateTime { get; set; }
        /// <summary>最后更新人。</summary>
        public Operator? UpdateBy { get; set; }
        /// <summary>最后更新时间（Unix 毫秒）。</summary>
        public long? UpdateTime { get; set; }

        /// <summary>是否已逻辑删除。</summary>
        public bool DeleteFlag { get; set; }= false;
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

    /// <summary>包含动态 data 对象的表单实体。</summary>
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

        /// <summary>动态表单字段值对象，字段结构由 FormDef 定义。</summary>
        public ExpandoObject Data { get; set; } = new ExpandoObject { };
    }
}
