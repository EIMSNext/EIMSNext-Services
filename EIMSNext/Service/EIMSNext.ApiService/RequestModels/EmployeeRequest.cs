namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 员工请求
    /// </summary>
    public class EmployeeRequest : RequestBase
    {
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
        /// 员工所属部门关系。每项包含部门 ID、负责人标志和排序值。
        /// </summary>
        public List<EmployeeDepartmentRequest> Departments { get; set; } = [];

        /// <summary>
        /// 邀请电话或Email
        /// </summary>
        public string? Invite { get; set; }
    }

    /// <summary>
    /// 员工与部门的关联请求。
    /// </summary>
    public class EmployeeDepartmentRequest
    {
        /// <summary>
        /// 部门 ID。
        /// </summary>
        public string DepartmentId { get; set; } = "";

        /// <summary>
        /// 是否为部门负责人。
        /// </summary>
        public bool IsManager { get; set; }

        /// <summary>
        /// 部门内排序值。
        /// </summary>
        public int SortValue { get; set; }
    }
}
