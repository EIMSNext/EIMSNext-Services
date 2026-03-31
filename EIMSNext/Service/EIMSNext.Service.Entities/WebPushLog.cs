using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// Web推送日志
    /// </summary>
    public class WebPushLog : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = "";

        /// <summary>
        /// 
        /// </summary>
        public string WebHookId { get; set; } = "";

        /// <summary>
        /// 
        /// </summary>
        public string SourceType { get; set; } = WebHookSource.Form;

        /// <summary>
        /// 
        /// </summary>
        public string TriggerType { get; set; } = "unknown";
        /// <summary>
        /// 推送地址
        /// </summary>
        public string Url { get; set; } = "";
        /// <summary>
        /// 事件ID
        /// </summary>
        public string EventId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string? PushObject { get; set; }

        /// <summary>
        /// 响应代码
        /// </summary>
        public int HttpCode { get; set; }
        /// <summary>
        /// 推送结果
        /// </summary>
        public string? PushResult { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success {  get; set; }
    }
}
