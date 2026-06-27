using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
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
    }
}
