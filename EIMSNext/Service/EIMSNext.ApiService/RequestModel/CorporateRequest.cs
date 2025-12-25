namespace EIMSNext.ApiService.RequestModel
{
    public class CorporateRequest : RequestBase
    {
        /// <summary>
        /// 企业名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 企业简介
        /// </summary>
        public string Description { get; set; } = "";
    }
}

