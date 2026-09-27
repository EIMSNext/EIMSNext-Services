namespace EIMSNext.ApiService
{
    /// <summary>
    /// 公开访问 scope，标识 token 可访问的公开资源类型。
    /// </summary>
    [Flags]
    public enum PublicScope
    {
        None = 0,

        /// <summary>
        /// 仪表盘公共读/聚合 (DashboardDef / DashboardItemDef / Aggregate)
        /// </summary>
        DashLink = 1,

        /// <summary>
        /// 表单提交 (FormData POST)
        /// </summary>
        FormLink = 2,

        /// <summary>
        /// 单条记录查看 (FormData GET by key)
        /// </summary>
        DataLink = 4,

        /// <summary>
        /// 列表查询 (FormData dynamic query)
        /// </summary>
        QueryLink = 8,
    }
}
