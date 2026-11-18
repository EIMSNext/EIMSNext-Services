using EIMSNext.Core.Abstractions;

namespace EIMSNext.Core.Entities
{
    /// <summary>
    /// 日志实体公共字段：与 <see cref="CorpEntityBase"/> 的 Id / CreateBy / CreateTime / CorpId 语义完全一致，
    /// 仅去掉日志不需要的逻辑删除与更新字段。日志只追加，不修改、不软删除。
    /// </summary>
    public abstract class LogEntityBase : KeyedEntityBase, ICorpOwned, IHasCreateTime
    {
        /// <summary>创建人。</summary>
        public Operator? CreateBy { get; set; }

        /// <summary>创建时间（Unix 毫秒）。</summary>
        public long CreateTime { get; set; }

        /// <summary>企业 ID。</summary>
        public string? CorpId { get; set; }
    }
}
