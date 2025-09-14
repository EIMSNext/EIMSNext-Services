namespace EIMSNext.ApiService.RequestModel
{
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
    }
}
