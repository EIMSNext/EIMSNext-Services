using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 企业
    /// </summary>
    public class Corporate : EntityBase
    {
        /// <summary>
        /// 企业名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 企业简介
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 企业编码， yyyyMMdd+2位平台编码+四位流水
        /// </summary>
        public string Code { get; set; } = "";
        /// <summary>
        /// 注册来源
        /// </summary>
        public PlatformType Platform { get; set; }
    }    
}
