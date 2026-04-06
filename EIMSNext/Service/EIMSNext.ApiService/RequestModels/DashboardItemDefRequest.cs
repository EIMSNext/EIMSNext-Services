namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 仪表盘项目定义请求
    /// </summary>
    public class DashboardItemDefRequest : RequestBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 仪表盘Id
        /// </summary>
        public string DashboardId { get; set; } = string.Empty;

        /// <summary>
        /// 项目类型
        /// </summary>
        public string ItemType { get; set; } = string.Empty;

        /// <summary>
        /// 布局Id
        /// </summary>
        public string LayoutId { get; set; } = string.Empty;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 详细设置
        /// </summary>
        public string Details { get; set; } = string.Empty;
    }
}
