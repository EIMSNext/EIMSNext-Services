namespace EIMSNext.Core.Entity
{
    public interface IUser
    {
        string Id { get; set; }
        string Name { get; }
        string? Email { get; }
        string? Phone { get; }
        PlatformType Platform { get; }
    }

    public interface IEmployee
    {
        /// <summary>
        /// 企业Id
        /// </summary>
        string? CorpId { get; }
        /// <summary>
        /// 员工ID
        /// </summary>
        string Id { get; }
        /// <summary>
        /// 对应用户ID
        /// </summary>
        string UserId { get; }
        /// <summary>
        /// 在当前企业的员工编码
        /// </summary>
        string Code { get; }
        /// <summary>
        /// 在当前企业的员工名称
        /// </summary>
        string EmpName { get; }

        Operator ToOperator();
    }
    /// <summary>
    /// 企业注册来源
    /// </summary>
    public enum PlatformType
    {
        /// <summary>
        /// 官网
        /// </summary>
        Public = 0,
        /// <summary>
        /// 企微
        /// </summary>
        Wxwork = 1,
        /// <summary>
        /// 钉钉
        /// </summary>
        Ding = 2,
        /// <summary>
        /// 飞书
        /// </summary>
        Feishu = 3
    }
}
