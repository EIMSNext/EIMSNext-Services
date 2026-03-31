using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
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
        public long Triggers { get; set; }
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }
}

