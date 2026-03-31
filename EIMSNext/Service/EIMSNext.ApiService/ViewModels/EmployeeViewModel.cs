using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    public class EmployeeViewModel : Employee
    {
        public Department? Department { get; set; }
    }
}

