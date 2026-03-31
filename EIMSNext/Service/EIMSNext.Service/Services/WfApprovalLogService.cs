using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
	public class WfApprovalLogService(IResolver resolver) : EntityServiceBase<Wf_ApprovalLog>(resolver), IWfApprovalLogService
	{
	}
}
