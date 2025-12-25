using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;

namespace EIMSNext.ServiceApi.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class WfDefinitionController(IResolver resolver) : ApiControllerBase<WfDefinitionApiService, Wf_Definition, WfDefinitionViewModel>(resolver)
	{
		
	}
}
