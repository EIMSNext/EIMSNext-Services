using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Api.OData;
using HKH.Mef2.Integration;

namespace EIMSNext.Service.Api.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class WfExecLogController(IResolver resolver) : ReadOnlyODataController<WfExecLogApiService, Wf_ExecLog, WfExecLogViewModel>(resolver)
	{
		
	}
}
