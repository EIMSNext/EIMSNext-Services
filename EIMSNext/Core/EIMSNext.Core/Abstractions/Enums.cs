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
        None,

        /// <summary>
        /// 新增。
        /// </summary>
        Insert,

        /// <summary>
        /// 更新。
        /// </summary>
        Update,

        /// <summary>
        /// 删除（逻辑删除）。
        /// </summary>
        Delete,

        /// <summary>
        /// 物理删除。
        /// </summary>
        PhysicalDelete
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
        Draft,
        /// <summary>
        /// 审批中
        /// </summary>
        Approving,
        /// <summary>
        /// 已审批
        /// </summary>
        Approved,
        /// <summary>
        /// 已驳回
        /// </summary>
        Rejected,
        /// <summary>
        /// 已挂起
        /// </summary>
        Suspended,
        /// <summary>
        /// 已废弃
        /// </summary>
        Discarded
    }
}
