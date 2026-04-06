namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 仪表盘定义请求
    /// </summary>
    public class DashboardDefRequest : RequestBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 仪表盘名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 布局
        /// </summary>
        public string Layout { get; set; } = string.Empty;
    }
}
