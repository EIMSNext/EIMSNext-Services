using EIMSNext.ApiService;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.ServiceApi.Authorization
{
    /// <summary>
    /// 身份配置
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class IdentityTypeAttribute : TypeFilterAttribute
    {
        /// <summary>
        /// 身份类型
        /// </summary>
        public IdentityType IdentityType { get; set; } = IdentityType.Anonymous;

        /// <summary>
        /// 
        /// </summary>
        public IdentityTypeAttribute(IdentityType identityType) : base(typeof(IdentityTypeFilter))
        {
            this.IdentityType = identityType;
        }
    }
}