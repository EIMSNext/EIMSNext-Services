using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

namespace EIMSNext.ApiService
{
	public class WfTodoApiService(IResolver resolver) : ApiServiceBase<Wf_Todo, WfTodoViewModel, IWfTodoService>(resolver)
	{
	}
}
