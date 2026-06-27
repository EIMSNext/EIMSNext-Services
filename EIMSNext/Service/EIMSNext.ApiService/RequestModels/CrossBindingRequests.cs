namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 跨应用绑定请求。
    /// </summary>
    public class CrossBindingRequest : RequestBase
    {
        public string TargetAppId { get; set; } = string.Empty;

        public string SourceAppId { get; set; } = string.Empty;

        public string SourceFormId { get; set; } = string.Empty;
    }
}
