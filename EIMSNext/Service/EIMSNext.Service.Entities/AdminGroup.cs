using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 管理组
    /// </summary>
    public class AdminGroup : CorpEntityBase
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
        public AdminGroupType Type { get; set; } = AdminGroupType.Normal;
        /// <summary>
        /// 父级分组ID，空表示根级
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
        public ScopeMode AppDepartmentScopeMode { get; set; } = ScopeMode.All;
        /// <summary>
        /// 应用内可选部门ID列表
        /// </summary>
        public List<string> AppDepartmentIds { get; set; } = [];
        /// <summary>
        /// 应用内可选角色范围模式
        /// </summary>
        public ScopeMode AppRoleScopeMode { get; set; } = ScopeMode.All;
        /// <summary>
        /// 应用内可选角色ID列表
        /// </summary>
        public List<string> AppRoleIds { get; set; } = [];
        /// <summary>
        /// 通讯录部门权限
        /// </summary>
        public PermissionLevel ContactDepartmentPermission { get; set; } = PermissionLevel.None;
        /// <summary>
        /// 通讯录部门范围模式
        /// </summary>
        public ScopeMode ContactDepartmentScopeMode { get; set; } = ScopeMode.All;
        /// <summary>
        /// 通讯录部门ID列表
        /// </summary>
        public List<string> ContactDepartmentIds { get; set; } = [];
        /// <summary>
        /// 通讯录角色权限
        /// </summary>
        public PermissionLevel ContactRolePermission { get; set; } = PermissionLevel.None;
        /// <summary>
        /// 通讯录角色范围模式
        /// </summary>
        public ScopeMode ContactRoleScopeMode { get; set; } = ScopeMode.All;
        /// <summary>
        /// 通讯录角色ID列表
        /// </summary>
        public List<string> ContactRoleIds { get; set; } = [];
    }

    /// <summary>
    /// 管理组类型
    /// </summary>
    public enum AdminGroupType
    {
        /// <summary>
        /// 普通管理组
        /// </summary>
        Normal = 0,
        /// <summary>
        /// 管理分组
        /// </summary>
        Folder = 1,
        /// <summary>
        /// 系统管理员组
        /// </summary>
        System = 2,
    }

    /// <summary>
    /// 范围模式
    /// </summary>
    public enum ScopeMode
    {
        /// <summary>
        /// 全部
        /// </summary>
        All = 0,
        /// <summary>
        /// 部分
        /// </summary>
        Partial = 1,
    }

    /// <summary>
    /// 权限级别
    /// </summary>
    public enum PermissionLevel
    {
        /// <summary>
        /// 无权限
        /// </summary>
        None = 0,
        /// <summary>
        /// 可见
        /// </summary>
        View = 1,
        /// <summary>
        /// 可管理
        /// </summary>
        Manage = 2,
    }
}
