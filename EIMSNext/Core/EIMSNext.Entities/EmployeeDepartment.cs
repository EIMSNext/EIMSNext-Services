using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 员工与部门的归属关系。
    /// 一名员工可隶属于多个部门；每个归属关系可标记是否为主部门管理者，并支持手动排序。
    /// </summary>
    public class EmployeeDepartment : CorpEntityBase
    {
        /// <summary>员工 ID。</summary>
        public string EmployeeId { get; set; } = "";

        /// <summary>部门 ID。</summary>
        public string DepartmentId { get; set; } = "";

        /// <summary>该员工在此部门是否担任管理者。</summary>
        public bool IsManager { get; set; }

        /// <summary>在同一部门内对归属关系的显示排序（值越小越靠前）。</summary>
        public int SortValue { get; set; }

        /// <summary>
        /// 部门层级路径快照（与 <see cref="Department.HeriarchyId"/> 同构，格式：|parentId|...|deptId|）。
        /// 创建归属关系时从部门写入；部门层级变动时由 <c>DepartmentService</c> 同步刷新。
        /// 级联按部门查员工时直接对本列做 Contains，避免 EmployeeDepartment → Department 的导航/联表。
        /// </summary>
        public string HeriarchyId { get; set; } = "";

        /// <summary>
        /// 关联部门导航，仅用于关系表查询中的层级路径过滤。
        /// </summary>
        public Department? Department { get; set; }
    }
}
