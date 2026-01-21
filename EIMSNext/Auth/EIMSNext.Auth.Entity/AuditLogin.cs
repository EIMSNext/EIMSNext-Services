using EIMSNext.Core.Entity;

namespace EIMSNext.Auth.Entity
{
    /// <summary>
    /// 登录日志
    /// </summary>
    public class AuditLogin : CorpEntityBase
    {
        /// <summary>
        /// 登录账号名，可能是电话号码，邮箱或第三方OpenId
        /// </summary>
        public string? LoginId { get; set; }
        /// <summary>
        /// 授权类型
        /// </summary>
        public string? GrantType { get; set; }
        /// <summary>
        /// 用户Id
        /// </summary>
        public string? UserId { get; set; }
        /// <summary>
        /// 客户端Id
        /// </summary>
        public string? ClientId { get; set; }
        /// <summary>
        /// 用户名或客户端名
        /// </summary>
        public string? UserName { get; set; }
        /// <summary>
        /// 客户端IP
        /// </summary>
        public string? ClientIp { get; set; }
        /// <summary>
        /// 失败原因， 为空时表示成功
        /// </summary>
        public string? FailReason { get; set; }
    }
}
