using HKH.Mef2.Integration;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

namespace EIMSNext.Service
{
	public class WfApprovalLogService(IResolver resolver) : EntityServiceBase<Wf_ApprovalLog>(resolver), IWfApprovalLogService
	{
	}
}
