namespace EIMSNext.Core
{
    public enum DbAction
    {
        None,
        Insert,
        Update,
        Delete
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
