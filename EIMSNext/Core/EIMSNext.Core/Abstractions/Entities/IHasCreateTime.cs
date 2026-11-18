namespace EIMSNext.Core.Abstractions
{
    /// <summary>具备创建时间戳的实体，供按 (CreateTime, Id) 游标分页的导出复用。</summary>
    public interface IHasCreateTime : IEntityKey
    {
        /// <summary>创建时间（Unix 毫秒）。</summary>
        long CreateTime { get; set; }
    }
}
