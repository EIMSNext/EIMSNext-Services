using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// Employee and department membership relation.
    /// </summary>
    public class EmployeeDepartment : CorpEntityBase
    {
        public string EmployeeId { get; set; } = "";

        public string DepartmentId { get; set; } = "";

        public bool IsManager { get; set; }

        public int SortValue { get; set; }
    }
}
