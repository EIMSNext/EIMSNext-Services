using EIMSNext.Core.Entity;

namespace EIMSNext.Auth.Entity
{
    /// <summary>
    /// 登录日志
    /// </summary>
    public class AuditLogin : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string UserId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string EmpId { get; set; } = "";
    }
}
