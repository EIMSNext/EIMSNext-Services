namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 管理组请求
    /// </summary>
    public class TenantAdminGroupRequest : RequestBase
    {
        /// <summary>
        /// 管理组名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 管理组描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// 管理组类型
        /// </summary>
        public EIMSNext.Entities.TenantAdminGroupType Type { get; set; } = EIMSNext.Entities.TenantAdminGroupType.Normal;
        /// <summary>
        /// 父级分组ID
        /// </summary>
        public string ParentId { get; set; } = string.Empty;
        /// <summary>
        /// 排序值
        /// </summary>
        public int SortValue { get; set; }
        /// <summary>
        /// 绑定员工ID列表
        /// </summary>
        public List<string> EmployeeIds { get; set; } = [];
        /// <summary>
        /// 可编辑应用ID列表
        /// </summary>
        public List<string> AppIds { get; set; } = [];
        /// <summary>
        /// 是否可添加/删除应用
        /// </summary>
        public bool CanCreateOrDeleteApp { get; set; }
        /// <summary>
        /// 应用内可选部门范围模式
        /// </summary>
        public EIMSNext.Entities.ScopeMode AppDepartmentScopeMode { get; set; } = EIMSNext.Entities.ScopeMode.All;
        /// <summary>
        /// 应用内可选部门ID列表
        /// </summary>
        public List<string> AppDepartmentIds { get; set; } = [];
        /// <summary>
        /// 应用内可选员工组范围模式
        /// </summary>
        public EIMSNext.Entities.ScopeMode AppEmployeeGroupScopeMode { get; set; } = EIMSNext.Entities.ScopeMode.All;
        /// <summary>
        /// 应用内可选员工组ID列表
        /// </summary>
        public List<string> AppEmployeeGroupIds { get; set; } = [];
        /// <summary>
        /// 通讯录部门权限
        /// </summary>
        public EIMSNext.Entities.PermissionLevel ContactDepartmentPermission { get; set; } = EIMSNext.Entities.PermissionLevel.None;
        /// <summary>
        /// 通讯录部门范围模式
        /// </summary>
        public EIMSNext.Entities.ScopeMode ContactDepartmentScopeMode { get; set; } = EIMSNext.Entities.ScopeMode.All;
        /// <summary>
        /// 通讯录部门ID列表
        /// </summary>
        public List<string> ContactDepartmentIds { get; set; } = [];
        /// <summary>
        /// 通讯录员工组权限
        /// </summary>
        public EIMSNext.Entities.PermissionLevel ContactEmployeeGroupPermission { get; set; } = EIMSNext.Entities.PermissionLevel.None;
        /// <summary>
        /// 通讯录员工组范围模式
        /// </summary>
        public EIMSNext.Entities.ScopeMode ContactEmployeeGroupScopeMode { get; set; } = EIMSNext.Entities.ScopeMode.All;
        /// <summary>
        /// 通讯录员工组ID列表
        /// </summary>
        public List<string> ContactEmployeeGroupIds { get; set; } = [];
    }

    /// <summary>
    /// 移动管理组请求
    /// </summary>
    public class MoveTenantAdminGroupRequest
    {
        /// <summary>
        /// 被移动管理组ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 新父级分组ID，空表示根级
        /// </summary>
        public string ParentId { get; set; } = string.Empty;
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

