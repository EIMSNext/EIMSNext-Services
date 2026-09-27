using EIMSNext.ApiService;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Authorization
{
    /// <summary>
    /// 身份配置
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
    public class IdentityTypeAttribute : TypeFilterAttribute
    {
        /// <summary>
        /// 身份类型
        /// </summary>
        public IdentityType IdentityType { get; set; } = IdentityType.None;

        /// <summary>
        /// 
        /// </summary>
        public IdentityTypeAttribute(IdentityType identityType) : base(typeof(IdentityTypeFilter))
        {
            this.IdentityType = identityType;
        }
    }

    internal static class IdentityTypeDefaults
    {
        /// <summary>
        /// 平台管理员
        /// </summary>
        public const IdentityType PlatAdmin = IdentityType.PlatAdmin;

        /// <summary>
        /// 企业所有者 + 超管 + 系统身份
        /// </summary>
        public const IdentityType CorpAdmin =
            IdentityType.System |
            IdentityType.Client |
            IdentityType.CorpOwmer |
            IdentityType.CorpAdmin;

        /// <summary>
        /// CorpAdmin + 应用子管理员
        /// </summary>
        public const IdentityType AppAdmin = CorpAdmin | IdentityType.AppAdmin;

        /// <summary>
        /// AppAdmin + 业务用户（普通员工）
        /// </summary>
        public const IdentityType BusinessUser =
            AppAdmin |
            IdentityType.FormAdmin |
            IdentityType.Employee;

        /// <summary>
        /// BusinessUser + 公开用户
        /// </summary>
        public const IdentityType PublicBusinessUser = BusinessUser | IdentityType.Public;

        /// <summary>
        /// 所有已认证用户（包括 NoCorp）
        /// </summary>
        public const IdentityType Authenticated = BusinessUser | IdentityType.NoCorp;
    }
}
