using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 当前员工的普通管理组权限快照。
    /// </summary>
    public class AdminPermissionSnapshot
    {
        /// <summary>是否为普通管理员。</summary>
        public bool IsNormalAdmin { get; set; }

        /// <summary>是否可以创建或删除应用。</summary>
        public bool CanCreateOrDeleteApp { get; set; }

        /// <summary>可管理的应用 ID 列表。</summary>
        public List<string> ManageableAppIds { get; set; } = [];

        /// <summary>可删除的应用 ID 列表。</summary>
        public List<string> DeletableAppIds { get; set; } = [];

        /// <summary>应用部门范围模式。</summary>
        public string AppDepartmentScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        /// <summary>应用部门 ID 列表。</summary>
        public List<string> AppDepartmentIds { get; set; } = [];

        /// <summary>应用员工组范围模式。</summary>
        public string AppEmployeeGroupScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        /// <summary>应用员工组 ID 列表。</summary>
        public List<string> AppEmployeeGroupIds { get; set; } = [];

        /// <summary>联系人查看部门范围模式。</summary>
        public string ContactViewDepartmentScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        /// <summary>联系人查看部门 ID 列表。</summary>
        public List<string> ContactViewDepartmentIds { get; set; } = [];

        /// <summary>联系人管理部门范围模式。</summary>
        public string ContactManageDepartmentScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        /// <summary>联系人管理部门 ID 列表。</summary>
        public List<string> ContactManageDepartmentIds { get; set; } = [];

        /// <summary>联系人查看员工组范围模式。</summary>
        public string ContactViewEmployeeGroupScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        /// <summary>联系人查看员工组 ID 列表。</summary>
        public List<string> ContactViewEmployeeGroupIds { get; set; } = [];

        /// <summary>联系人管理员工组范围模式。</summary>
        public string ContactManageEmployeeGroupScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        /// <summary>联系人管理员工组 ID 列表。</summary>
        public List<string> ContactManageEmployeeGroupIds { get; set; } = [];

        /// <summary>
        /// 将范围模式转换为传输用的字符串。
        /// </summary>
        /// <param name="mode">范围模式。</param>
        /// <returns>范围模式对应的字符串表示。</returns>
        public static string ToWireScopeMode(ScopeMode mode)
        {
            return ((int)mode).ToString();
        }
    }
}
