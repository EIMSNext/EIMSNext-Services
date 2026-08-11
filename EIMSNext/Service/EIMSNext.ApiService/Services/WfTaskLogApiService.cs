using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
	public class WfTaskLogApiService(IResolver resolver) : ApiServiceBase<Wf_TaskLog, WfTaskLogViewModel, IWfTaskLogService>(resolver)
	{
	}
}
