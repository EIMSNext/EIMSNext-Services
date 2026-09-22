using System.Dynamic;
using System.Text.Json;

using EIMSNext.Core.Abstractions;

namespace EIMSNext.Core.Entities
{
    /// <summary>
    /// 持久化实体公共字段。
    /// <para>
    /// 主键由 PostgreSQL/EF Core 的 Fluent API 映射为 <c>text</c> 列；
    /// <see cref="Id"/> 的 <c>string</c> 契约保持不变（未迁移为整型）。
    /// </para>
    /// </summary>
    public abstract class KeyedEntityBase : IEntityKey
    {
        /// <summary>资源标识。</summary>
        public string Id { get; set; } = string.Empty;
    }
    /// <summary>包含审计字段的实体基类。</summary>
    public abstract class EntityBase : KeyedEntityBase, IEntity
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
        /// <summary>
        /// 初始化 <see cref="DynamicEntity"/> 类的新实例。
        /// </summary>
        public DynamicEntity()
        {
        }

        /// <summary>
        /// 使用 JSON 数据初始化 <see cref="DynamicEntity"/> 类的新实例。
        /// </summary>
        /// <param name="dataJson">动态数据 JSON。</param>
        //测试用方法
        public DynamicEntity(string dataJson)
        {
            if (!string.IsNullOrEmpty(dataJson))
                Data = dataJson.DeserializeFromJson<Dictionary<string, object?>>()!;
        }

        /// <summary>动态表单字段值对象，字段结构由 FormDef 定义。</summary>
        /// <remarks>
        /// 原 <see cref="ExpandoObject"/>，改 Dictionary 的依据：读路径基准 43µs/行 vs 91µs/行，
        /// 且全库对 Data 的访问全部走 <c>IDictionary&lt;string, object?&gt;</c> 接口，
        /// ExpandoObject 的 DLR 元对象支持从未被 <c>dynamic</c> 用到（见 .workbuddy/memory/2026-09-20.md）。
        /// </remarks>
        public Dictionary<string, object?> Data { get; set; } = new Dictionary<string, object?> { };
    }
}
