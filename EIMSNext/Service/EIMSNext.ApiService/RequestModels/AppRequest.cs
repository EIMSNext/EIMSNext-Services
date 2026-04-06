namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 应用请求
    /// </summary>
    public class AppRequest : RequestBase
    {
        /// <summary>
        /// 应用名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 应用描述
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 应用图标
        /// </summary>
        public string Icon { get; set; } = "";
        /// <summary>
        /// 图标颜色
        /// </summary>
        public string IconColor { get; set; } = "";
        /// <summary>
        /// 排序索引
        /// </summary>
        public int SortIndex { get; set; }
    }
}
