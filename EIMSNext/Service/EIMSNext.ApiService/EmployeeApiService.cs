using HKH.Mef2.Integration;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Service.Interface;

namespace EIMSNext.ApiService
{
	public class EmployeeApiService(IResolver resolver) : ApiServiceBase<Employee, EmployeeViewModel, IEmployeeService>(resolver)
	{
	}
}
