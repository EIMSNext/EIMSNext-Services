namespace EIMSNext.Core.Abstractions
{
    /// <summary>
    /// 用户接口，定义用户的基础信息。
    /// </summary>
    public interface IUser
    {
        /// <summary>
        /// 获取或设置用户 ID。
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// 获取用户名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取用户邮箱。
        /// </summary>
        string? Email { get; }

        /// <summary>
        /// 获取用户手机号。
        /// </summary>
        string? Phone { get; }

        /// <summary>
        /// 获取用户所属平台。
        /// </summary>
        PlatformType Platform { get; }
    }

    /// <summary>
    /// 员工接口，定义员工在企业维度的信息。
    /// </summary>
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

        /// <summary>
        /// 转换为操作者对象。
        /// </summary>
        /// <returns>操作者对象。</returns>
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
        Feishu = 3,
        /// <summary>
        /// 私有
        /// </summary>
        Private = 999
    }
}
