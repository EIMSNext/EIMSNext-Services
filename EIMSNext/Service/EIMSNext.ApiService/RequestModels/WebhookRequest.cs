using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// WebHook配置请求
    /// </summary>
    public class WebhookRequest : RequestBase
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
        /// 推送地址
        /// </summary>
        public string Url { get; set; } = "";
        /// <summary>
        /// Secret密钥
        /// </summary>
        public string Secret { get; set; } = "";

        /// <summary>
        /// WebHook触发模式
        /// </summary>
        public long Triggers { get; set; }
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }
}
