using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;

namespace EIMSNext.ServiceApi.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class WfApprovalLogController(IResolver resolver) : ApiControllerBase<Wf_ApprovalLog, Wf_ApprovalLog>(resolver)
	{
		
	}
}
