using EIMSNext.Entity;

namespace EIMSNext.ApiService.RequestModel
{
    public class WebhookRequest : RequestBase
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
        /// 推送地址
        /// </summary>
        public string Url { get; set; } = "";
        /// <summary>
        /// Secret
        /// </summary>
        public string Secret { get; set; } = "";

        /// <summary>
        /// 
        /// </summary>
        public WebHookTrigger Triggers { get; set; }
    }
}

