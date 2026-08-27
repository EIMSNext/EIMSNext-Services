namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 角色请求
    /// </summary>
    public class EmployeeGroupRequest : RequestBase
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
        public string EmployeeGroupCategoryId { get; set; } = "";
        /// <summary>
        /// 排序值
        /// </summary>
        public int SortValue { get; set; }
    }

    /// <summary>
    /// 添加员工到角色请求
    /// </summary>
    public class AddEmployeesToEmployeeGroupRequest
    {
        /// <summary>
        /// 角色ID
        /// </summary>
        public string? EmployeeGroupId { get; set; }
        /// <summary>
        /// 员工ID列表
        /// </summary>
        public List<string>? EmpIds { get; set; }
    }
    /// <summary>
    /// 从角色中移除员工请求
    /// </summary>
    public class RemoveEmployeesFromEmployeeGroupRequest
    {
        /// <summary>
        /// 角色ID
        /// </summary>
        public string? EmployeeGroupId { get; set; }
        /// <summary>
        /// 员工ID列表
        /// </summary>
        public List<string>? EmpIds { get; set; }
    }

    /// <summary>
    /// 移动角色树节点请求
    /// </summary>
    public class MoveEmployeeGroupTreeNodeRequest
    {
        /// <summary>
        /// 被移动节点ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 是否角色组
        /// </summary>
        public bool IsGroup { get; set; }
        /// <summary>
        /// 新角色组ID，空表示根级
        /// </summary>
        public string EmployeeGroupCategoryId { get; set; } = string.Empty;
        /// <summary>
        /// 移动后前一个同级节点ID
        /// </summary>
        public string PreviousId { get; set; } = string.Empty;
        /// <summary>
        /// 移动后后一个同级节点ID
        /// </summary>
        public string NextId { get; set; } = string.Empty;
    }
}
