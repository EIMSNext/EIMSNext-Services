using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 企业入驻申请。
    /// </summary>
    public class CorpOnboardingRequest : EntityBase
    {
        /// <summary>
        /// 申请用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 申请用户名称。
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 申请加入的企业标识。
        /// </summary>
        public string TargetCorpId { get; set; } = string.Empty;

        /// <summary>
        /// 申请加入的企业名称。
        /// </summary>
        public string TargetCorpName { get; set; } = string.Empty;

        /// <summary>
        /// 申请人姓名。
        /// </summary>
        public string ApplicantName { get; set; } = string.Empty;

        /// <summary>
        /// 申请人手机号。
        /// </summary>
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// 申请人邮箱。
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 关联的待审批员工标识。
        /// </summary>
        public string EmployeeId { get; set; } = string.Empty;

        /// <summary>
        /// 入企来源，管理员邀请或用户申请。
        /// </summary>
        public string SourceType { get; set; } = CorpOnboardingSourceType.UserApply;
    }

    /// <summary>
    /// 入企来源类型。
    /// </summary>
    public static class CorpOnboardingSourceType
    {
        /// <summary>
        /// 管理员邀请员工加入。
        /// </summary>
        public const string AdminInvite = "AdminInvite";

        /// <summary>
        /// 用户申请加入企业。
        /// </summary>
        public const string UserApply = "UserApply";
    }
}
