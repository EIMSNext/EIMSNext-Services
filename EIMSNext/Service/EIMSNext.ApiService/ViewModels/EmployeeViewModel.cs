using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 员工视图模型。
    /// </summary>
    public class EmployeeViewModel : Employee
    {
    }

    /// <summary>
    /// 部门引用。
    /// </summary>
    public class DepartmentRef
    {
        /// <summary>部门 ID。</summary>
        public string Id { get; set; } = "";

        /// <summary>部门名称。</summary>
        public string Name { get; set; } = "";

        /// <summary>是否为管理者。</summary>
        public bool IsManager { get; set; }

        /// <summary>排序值。</summary>
        public int SortValue { get; set; }
    }
}
