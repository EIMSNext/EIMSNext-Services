using EIMSNext.Entities;

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

        public string AppEmployeeGroupScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> AppEmployeeGroupIds { get; set; } = [];

        public string ContactViewDepartmentScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> ContactViewDepartmentIds { get; set; } = [];

        public string ContactManageDepartmentScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> ContactManageDepartmentIds { get; set; } = [];

        public string ContactViewEmployeeGroupScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> ContactViewEmployeeGroupIds { get; set; } = [];

        public string ContactManageEmployeeGroupScopeMode { get; set; } = ToWireScopeMode(ScopeMode.Partial);

        public List<string> ContactManageEmployeeGroupIds { get; set; } = [];

        public static string ToWireScopeMode(ScopeMode mode)
        {
            return ((int)mode).ToString();
        }
    }
}
