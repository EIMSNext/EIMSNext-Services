using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using System.Text.Json.Serialization;

namespace EIMSNext.Service.Entities
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
        /// 所属角色
        /// </summary>
        public List<EmpRole> Roles { get; set; } = new List<EmpRole>();

        /// <summary>
        /// 所属部门（嵌入式，用于OData查询优化）
        /// </summary>
        public List<EmpDept> Depts { get; set; } = new List<EmpDept>();

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
    /// 员工角色关联
    /// </summary>
    public class EmpRole
    {
        /// <summary>
        /// 角色ID
        /// </summary>
        public string RoleId { get; set; } = "";
        /// <summary>
        /// 角色名称
        /// </summary>
        public string RoleName { get; set; } = "";
    }

    /// <summary>
    /// 员工部门关联（嵌入式，用于OData查询优化）
    /// </summary>
    public class EmpDept
    {
        /// <summary>
        /// 部门ID
        /// </summary>
        public string DeptId { get; set; } = "";
        /// <summary>
        /// 部门层级路径，格式：|parentId|grandparentId|...
        /// </summary>
        [JsonIgnore]
        public string HeriarchyId { get; set; } = "";
        /// <summary>
        /// 部门名称
        /// </summary>
        public string DeptName { get; set; } = "";
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
