namespace EIMSNext.Core.Abstractions
{
    /// <summary>
    /// Mongo 实体接口，定义实体的主键标识。
    /// </summary>
    public interface IMongoEntity
    {
        /// <summary>
        /// 获取或设置实体主键 ID。
        /// </summary>
        string Id { get; set; }
    }

    /// <summary>
    /// 企业归属接口，定义实体的企业归属标识。
    /// </summary>
    public interface ICorpOwned
    {
        /// <summary>
        /// 获取或设置企业 ID。
        /// </summary>
        string? CorpId {  get; set; }
    }

    /// <summary>
    /// 逻辑删除标识接口。
    /// </summary>
    public interface IDeleteFlag
    {
        /// <summary>
        /// 获取或设置一个值，指示是否已逻辑删除。
        /// </summary>
        bool DeleteFlag { get; set; }
    }

    /// <summary>
    /// 完整实体接口，包含主键、审计字段与逻辑删除标识。
    /// </summary>
    public interface IEntity : IMongoEntity, IDeleteFlag
    {
        /// <summary>
        /// 获取或设置创建人。
        /// </summary>
        Operator? CreateBy { get; set; }

        /// <summary>
        /// 获取或设置创建时间（Unix 毫秒）。
        /// </summary>
        long CreateTime { get; set; }

        /// <summary>
        /// 获取或设置最后更新人。
        /// </summary>
        Operator? UpdateBy { get; set; }

        /// <summary>
        /// 获取或设置最后更新时间（Unix 毫秒）。
        /// </summary>
        long? UpdateTime { get; set; }
    }
}
