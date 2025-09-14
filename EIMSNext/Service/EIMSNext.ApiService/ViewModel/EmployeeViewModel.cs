using EIMSNext.Entity;

namespace EIMSNext.ApiService.ViewModel
{
    public class EmployeeViewModel : Employee
    {
        public Department? Department { get; set; }
    }
}

