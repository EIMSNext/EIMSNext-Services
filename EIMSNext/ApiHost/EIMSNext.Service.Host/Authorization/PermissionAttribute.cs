using EIMSNext.ApiService;
using EIMSNext.Common;

namespace EIMSNext.Service.Host.Authorization
{
    /// <summary>
    /// 权限配置
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
    public class PermissionAttribute : Attribute
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

    }
}
