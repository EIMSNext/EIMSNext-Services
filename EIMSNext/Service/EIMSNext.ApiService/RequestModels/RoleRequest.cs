namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 角色请求
    /// </summary>
    public class RoleRequest : RequestBase
    {
        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 角色描述
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 角色组ID
        /// </summary>
        public string RoleGroupId { get; set; } = "";
        /// <summary>
        /// 排序值
        /// </summary>
        public int SortValue { get; set; }
    }

    /// <summary>
    /// 添加员工到角色请求
    /// </summary>
    public class AddEmpsToRoleRequest
    {
        /// <summary>
        /// 角色ID
        /// </summary>
        public string? RoleId { get; set; }
        /// <summary>
        /// 员工ID列表
        /// </summary>
        public List<string>? EmpIds { get; set; }
    }
    /// <summary>
    /// 从角色中移除员工请求
    /// </summary>
    public class RemoveEmpsToRoleRequest
    {
        /// <summary>
        /// 角色ID
        /// </summary>
        public string? RoleId { get; set; }
        /// <summary>
        /// 员工ID列表
        /// </summary>
        public List<string>? EmpIds { get; set; }
    }
}
