using HKH.Mef2.Integration;

using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
	public class WfExecLogService(IResolver resolver) : MongoEntityServiceBase<Wf_ExecLog>(resolver), IWfExecLogService
	{
	}
}
