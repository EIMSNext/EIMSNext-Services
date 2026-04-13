using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class EmployeeController(IResolver resolver) : ApiControllerBase<EmployeeApiService, Employee, EmployeeViewModel>(resolver)
	{
		
	}
}
