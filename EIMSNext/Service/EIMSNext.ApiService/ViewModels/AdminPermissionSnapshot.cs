using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 当前员工的普通管理组权限快照。
    /// </summary>
    public class AdminPermissionSnapshot
    {
        public bool IsNormalAdmin { get; set; }

        public bool CanCreateOrDeleteApp { get; set; }

        public List<string> ManageableAppIds { get; set; } = [];

        public List<string> DeletableAppIds { get; set; } = [];

        public string AppDepartmentScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> AppDepartmentIds { get; set; } = [];

        public string AppRoleScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> AppRoleIds { get; set; } = [];

        public string ContactViewDepartmentScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> ContactViewDepartmentIds { get; set; } = [];

        public string ContactManageDepartmentScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> ContactManageDepartmentIds { get; set; } = [];

        public string ContactViewRoleScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> ContactViewRoleIds { get; set; } = [];

        public string ContactManageRoleScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> ContactManageRoleIds { get; set; } = [];

        public static string ToWireScopeMode(ScopeMode mode)
        {
            return ((int)mode).ToString();
        }
    }
}
