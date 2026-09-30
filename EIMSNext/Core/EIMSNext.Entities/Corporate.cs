using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;

namespace EIMSNext.Entities
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
        /// 注册来源
        /// </summary>
        public PlatformType Platform { get; set; }
    }    
}
