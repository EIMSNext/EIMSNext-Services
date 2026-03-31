using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Api.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class DashboardDefController(IResolver resolver) : ApiControllerBase<DashboardDefApiService, DashboardDef, DashboardDefViewModel>(resolver)
	{
		
	}
}
