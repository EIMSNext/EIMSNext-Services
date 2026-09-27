namespace EIMSNext.Core.Abstractions
{
    /// <summary>
    /// 数据库操作类型。
    /// </summary>
    public enum DbAction
    {
        /// <summary>
        /// 无操作。
        /// </summary>
        None = 0,

        /// <summary>
        /// 新增。
        /// </summary>
        Insert = 1,

        /// <summary>
        /// 更新。
        /// </summary>
        Update = 2,

        /// <summary>
        /// 删除（逻辑删除）。
        /// </summary>
        Delete = 3,

        /// <summary>
        /// 物理删除。
        /// </summary>
        PhysicalDelete = 4,
    }

    /// <summary>
    /// 流程状态
    /// </summary>
    public enum FlowStatus
    {
        /// <summary>
        /// 无状态
        /// </summary>
        None = 0,
        /// <summary>
        /// 草稿
        /// </summary>
        Draft = 1,
        /// <summary>
        /// 审批中
        /// </summary>
        Approving = 2,
        /// <summary>
        /// 已审批
        /// </summary>
        Approved = 3,
        /// <summary>
        /// 已驳回
        /// </summary>
        Rejected = 4,
        /// <summary>
        /// 已挂起
        /// </summary>
        Suspended = 5,
        /// <summary>
        /// 已废弃
        /// </summary>
        Discarded = 6,
    }
}
