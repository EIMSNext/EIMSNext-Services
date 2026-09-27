using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	/// <summary>
	/// 工作流任务的 API 服务。
	/// </summary>
	/// <param name="resolver">服务解析器。</param>
	public class WfTaskApiService(IResolver resolver) : ApiServiceBase<Wf_Task, WfTaskViewModel, IWfTaskService>(resolver)
	{
	}
}
