namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 应用市场发布状态。
    /// </summary>
    public enum AppProfileStatus
    {
        /// <summary>
        /// 草稿（创建但未发布，仅创建者可见）。
        /// </summary>
        Draft = 0,

        /// <summary>
        /// 已发布（应用市场公开浏览/安装）。
        /// </summary>
        Published = 1,

        /// <summary>
        /// 已下架（保留 Profile 但应用市场不再展示）。
        /// </summary>
        Offline = 2,
    }
}
