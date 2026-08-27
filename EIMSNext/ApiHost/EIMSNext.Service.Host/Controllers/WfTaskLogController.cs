using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class WfTaskLogController(IResolver resolver) : ApiControllerBase<WfTaskLogApiService, Wf_TaskLog, WfTaskLogViewModel>(resolver)
	{
		
	}
}
