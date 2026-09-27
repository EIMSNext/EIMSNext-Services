using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	/// <summary>
	/// 工作流执行日志的 API 服务。
	/// </summary>
	/// <param name="resolver">服务解析器。</param>
	public class WfExecLogApiService(IResolver resolver) : ApiServiceBase<Wf_ExecLog, WfExecLogViewModel, IWfExecLogService>(resolver)
	{
	}
}
