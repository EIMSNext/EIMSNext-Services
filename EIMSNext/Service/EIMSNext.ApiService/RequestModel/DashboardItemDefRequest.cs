namespace EIMSNext.ApiService.RequestModel
{
    public class DashboardItemDefRequest : RequestBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 仪表盘Id
        /// </summary>
        public string DashboardId { get; set; } = string.Empty;

        /// <summary>
        /// 
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

