using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
	public class WfApprovalLogApiService(IResolver resolver) : ApiServiceBase<Wf_ApprovalLog, WfApprovalLogViewModel, IWfApprovalLogService>(resolver)
	{
	}
}
