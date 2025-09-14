using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
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
        /// 在职状态， 0 在职， 1 离职
        /// </summary>
        public int Status { get; set; }
        /// <summary>
        /// 是否虚拟用户, 系统用户或匿名用户
        /// </summary>
        public bool IsDummy { get; set; } = false;

        /// <summary>
        /// 在当前企业的部门Id
        /// </summary>
        public string DepartmentId { get; set; } = "";

        /// <summary>
        /// 是否部门主管
        /// </summary>
        public bool IsManager { get; set; }

        /// <summary>
        /// 邀请电话或Email
        /// </summary>
        public string? Invite { get; set; }

        /// <summary>
        /// 是否已经绑定用户
        /// </summary>
        public bool Approved { get; set; }

        /// <summary>
        /// 所属角色
        /// </summary>
        public List<EmpRole> Roles { get; set; } = new List<EmpRole>();

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Operator ToOperator()
        {
            return new Operator(CorpId, UserId, Id, EmpName);
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsSystem => IsDummy && Id.StartsWith("system_");
        /// <summary>
        /// 
        /// </summary>
        public bool IsAnonymous => IsDummy && Id.StartsWith("anonymous_");
    }

    /// <summary>
    /// 
    /// </summary>
    public class EmpRole
    {
        /// <summary>
        /// 
        /// </summary>
        public string RoleId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string RoleName { get; set; } = "";
    }
}
