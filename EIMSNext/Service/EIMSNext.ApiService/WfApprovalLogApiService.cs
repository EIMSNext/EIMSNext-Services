using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
	public class WfApprovalLogApiService(IResolver resolver) : ApiServiceBase<Wf_ApprovalLog, WfApprovalLogViewModel>(resolver)
	{
	}
}
