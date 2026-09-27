using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
	/// <summary>
	/// 工作流任务日志的 API 服务。
	/// </summary>
	/// <param name="resolver">服务解析器。</param>
	public class WfTaskLogApiService(IResolver resolver) : ApiServiceBase<Wf_TaskLog, WfTaskLogViewModel, IWfTaskLogService>(resolver)
	{
	}
}
