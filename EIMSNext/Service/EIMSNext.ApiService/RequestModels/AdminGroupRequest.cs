namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 管理组请求
    /// </summary>
    public class AdminGroupRequest : RequestBase
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
        public EIMSNext.Service.Entities.AdminGroupType Type { get; set; } = EIMSNext.Service.Entities.AdminGroupType.Normal;
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
        public EIMSNext.Service.Entities.ScopeMode AppDepartmentScopeMode { get; set; } = EIMSNext.Service.Entities.ScopeMode.All;
        /// <summary>
        /// 应用内可选部门ID列表
        /// </summary>
        public List<string> AppDepartmentIds { get; set; } = [];
        /// <summary>
        /// 应用内可选角色范围模式
        /// </summary>
        public EIMSNext.Service.Entities.ScopeMode AppRoleScopeMode { get; set; } = EIMSNext.Service.Entities.ScopeMode.All;
        /// <summary>
        /// 应用内可选角色ID列表
        /// </summary>
        public List<string> AppRoleIds { get; set; } = [];
        /// <summary>
        /// 通讯录部门权限
        /// </summary>
        public EIMSNext.Service.Entities.PermissionLevel ContactDepartmentPermission { get; set; } = EIMSNext.Service.Entities.PermissionLevel.None;
        /// <summary>
        /// 通讯录部门范围模式
        /// </summary>
        public EIMSNext.Service.Entities.ScopeMode ContactDepartmentScopeMode { get; set; } = EIMSNext.Service.Entities.ScopeMode.All;
        /// <summary>
        /// 通讯录部门ID列表
        /// </summary>
        public List<string> ContactDepartmentIds { get; set; } = [];
        /// <summary>
        /// 通讯录角色权限
        /// </summary>
        public EIMSNext.Service.Entities.PermissionLevel ContactRolePermission { get; set; } = EIMSNext.Service.Entities.PermissionLevel.None;
        /// <summary>
        /// 通讯录角色范围模式
        /// </summary>
        public EIMSNext.Service.Entities.ScopeMode ContactRoleScopeMode { get; set; } = EIMSNext.Service.Entities.ScopeMode.All;
        /// <summary>
        /// 通讯录角色ID列表
        /// </summary>
        public List<string> ContactRoleIds { get; set; } = [];
    }

    /// <summary>
    /// 移动管理组请求
    /// </summary>
    public class MoveAdminGroupRequest
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
