namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 跨应用绑定请求。
    /// </summary>
    public class CrossBindingRequest : RequestBase
    {
        /// <summary>目标应用 ID。</summary>
        public string TargetAppId { get; set; } = string.Empty;

        /// <summary>来源应用 ID。</summary>
        public string SourceAppId { get; set; } = string.Empty;

        /// <summary>来源表单 ID。</summary>
        public string SourceFormId { get; set; } = string.Empty;
    }
}
