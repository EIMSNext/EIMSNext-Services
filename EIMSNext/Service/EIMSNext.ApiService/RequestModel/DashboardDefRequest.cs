namespace EIMSNext.ApiService.RequestModel
{
    public class DashboardDefRequest : RequestBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 布局
        /// </summary>
        public string Layout { get; set; } = string.Empty;
    }
}

