namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 员工组请求
    /// </summary>
    public class EmployeeGroupRequest : RequestBase
    {
        /// <summary>
        /// 员工组名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 员工组描述
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 员工组分类 ID
        /// </summary>
        public string EmployeeGroupCategoryId { get; set; } = "";
        /// <summary>
        /// 排序值
        /// </summary>
        public int SortValue { get; set; }
    }

    /// <summary>
    /// 添加员工到员工组请求
    /// </summary>
    public class AddEmployeesToEmployeeGroupRequest
    {
        /// <summary>
        /// 员工组 ID
        /// </summary>
        public string? EmployeeGroupId { get; set; }
        /// <summary>
        /// 员工ID列表
        /// </summary>
        public List<string>? EmpIds { get; set; }
    }
    /// <summary>
    /// 从员工组中移除员工请求
    /// </summary>
    public class RemoveEmployeesFromEmployeeGroupRequest
    {
        /// <summary>
        /// 员工组 ID
        /// </summary>
        public string? EmployeeGroupId { get; set; }
        /// <summary>
        /// 员工ID列表
        /// </summary>
        public List<string>? EmpIds { get; set; }
    }

    /// <summary>
    /// 移动员工组树节点请求
    /// </summary>
    public class MoveEmployeeGroupTreeNodeRequest
    {
        /// <summary>
        /// 被移动节点ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 是否员工组分类
        /// </summary>
        public bool IsGroup { get; set; }
        /// <summary>
        /// 新员工组分类 ID，空表示根级
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
