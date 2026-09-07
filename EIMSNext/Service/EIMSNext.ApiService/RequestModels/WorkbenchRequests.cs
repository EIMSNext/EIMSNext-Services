namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 工作台配置请求。
    /// </summary>
    public class WorkbenchConfigRequest : RequestBase
    {
        /// <summary>布局配置。</summary>
        public string Layout { get; set; } = string.Empty;

        /// <summary>页面样式。</summary>
        public string PageStyle { get; set; } = string.Empty;
    }

    /// <summary>
    /// 工作台收藏请求。
    /// </summary>
    public class WorkbenchFavoriteRequest : RequestBase
    {
        /// <summary>目标类型。</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>目标 ID。</summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>排序索引。</summary>
        public long SortIndex { get; set; }
    }

    /// <summary>
    /// 工作台最近访问请求。
    /// </summary>
    public class WorkbenchRecentVisitRequest : RequestBase
    {
        /// <summary>目标类型。</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>目标 ID。</summary>
        public string TargetId { get; set; } = string.Empty;
    }
}
