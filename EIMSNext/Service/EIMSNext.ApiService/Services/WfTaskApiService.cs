using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	public class WfTaskApiService(IResolver resolver) : ApiServiceBase<Wf_Task, WfTaskViewModel, IWfTaskService>(resolver)
	{
	}
}
