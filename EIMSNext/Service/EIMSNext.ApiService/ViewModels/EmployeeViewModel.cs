using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    public class EmployeeViewModel : Employee
    {
        public List<DepartmentRef> Departments { get; set; } = [];
    }

    public class DepartmentRef
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public bool IsManager { get; set; }

        public int SortValue { get; set; }
    }
}

