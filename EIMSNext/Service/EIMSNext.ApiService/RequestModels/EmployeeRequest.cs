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

        public List<EmployeeDepartmentRequest> Departments { get; set; } = [];

        /// <summary>
        /// 邀请电话或Email
        /// </summary>
        public string? Invite { get; set; }
    }

    public class EmployeeDepartmentRequest
    {
        public string DepartmentId { get; set; } = "";

        public bool IsManager { get; set; }

        public int SortValue { get; set; }
    }
}
