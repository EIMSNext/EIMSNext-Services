using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 员工
    /// </summary>
    public class Employee : CorpEntityBase, IEmployee
    {
        /// <summary>
        /// 相关用户ID
        /// </summary>
        public string UserId { get; set; } = "";
        /// <summary>
        /// 相关用户名称
        /// </summary>
        public string UserName { get; set; } = "";
        /// <summary>
        /// 在当前企业的员工编码
        /// </summary>
        public string Code { get; set; } = "";
        /// <summary>
        /// 在当前企业的员工名称
        /// </summary>
        public string EmpName { get; set; } = "";
        /// <summary>
        /// 工作电话
        /// </summary>
        public string WorkPhone { get; set; } = "";
        /// <summary>
        /// 工作邮箱
        /// </summary>
        public string WorkEmail { get; set; } = "";
        /// <summary>
        /// 员工状态，0 在职，1 离职，2 待审核。
        /// </summary>
        public int Status { get; set; }
        /// <summary>
        /// 是否虚拟用户, 系统用户或匿名用户
        /// </summary>
        public bool IsDummy { get; set; } = false;

        /// <summary>
        /// 邀请电话或Email
        /// </summary>
        public string? Invite { get; set; }

        /// <summary>
        /// 是否已完成用户绑定或账号确认。
        /// </summary>
        public bool UserBound { get; set; }

        /// <summary>
        /// 所属部门（关系表导航）。用于按部门 OData 过滤。
        /// 由 EF 约定依据 <see cref="EmployeeDepartment.EmployeeId"/> 形成外键。
        /// </summary>
        public List<EmployeeDepartment> Departments { get; set; } = new List<EmployeeDepartment>();

        /// <summary>
        /// 所属员工组（关系表导航）。用于按员工组 OData 过滤。
        /// 由 EF 约定依据 <see cref="EmployeeGroupMember.EmployeeId"/> 形成外键。
        /// </summary>
        public List<EmployeeGroupMember> Groups { get; set; } = new List<EmployeeGroupMember>();

        /// <summary>
        /// 转换为操作员对象
        /// </summary>
        /// <returns>操作员实例</returns>
        public Operator ToOperator()
        {
            return new Operator(Id, Code, EmpName);
        }

        /// <summary>
        /// 是否为系统用户
        /// </summary>
        public bool IsSystem => IsDummy && Id.Equals("system");
        /// <summary>
        /// 是否为匿名用户
        /// </summary>
        public bool IsAnonymous => IsDummy && Id.Equals("public");
    }

    /// <summary>
    /// 员工状态常量。
    /// </summary>
    public static class EmployeeStatus
    {
        /// <summary>
        /// 在职。
        /// </summary>
        public const int Active = 0;

        /// <summary>
        /// 离职。
        /// </summary>
        public const int Inactive = 1;

        /// <summary>
        /// 待审核。
        /// </summary>
        public const int PendingReview = 2;
    }
}
