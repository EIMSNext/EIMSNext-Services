using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// Web推送日志
    /// </summary>
    public class WebPushLog : CorpEntityBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = "";

        /// <summary>
        /// WebHook标识
        /// </summary>
        public string WebHookId { get; set; } = "";

        /// <summary>
        /// 来源类型
        /// </summary>
        public string SourceType { get; set; } = WebHookSource.Form;

        /// <summary>
        /// 触发类型
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
        /// 推送内容对象
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
