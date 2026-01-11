using EIMSNext.ApiService;
using EIMSNext.Common;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.ServiceApi.Authorization
{
    /// <summary>
    /// 权限配置
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class PermissionAttribute : TypeFilterAttribute
    {
        /// <summary>
        /// 资源代码
        /// </summary>
        public string ResourceCode { get; set; } = string.Empty;
        /// <summary>
        /// 访问控制级别
        /// </summary>
        public AccessControlLevel AccessControlLevel { get; set; } = AccessControlLevel.NotSet;
        /// <summary>
        /// 需要的操作权限
        /// </summary>
        public Operation Operation { get; set; } = Operation.NotSet;

        /// <summary>
        /// 
        /// </summary>
        public PermissionAttribute() : base(typeof(PermissionFilter))
        {
        }
    }
}
