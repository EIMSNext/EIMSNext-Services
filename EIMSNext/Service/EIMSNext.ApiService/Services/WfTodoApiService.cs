using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	public class WfTodoApiService(IResolver resolver) : ApiServiceBase<Wf_Todo, WfTodoViewModel, IWfTodoService>(resolver)
	{
	}
}
