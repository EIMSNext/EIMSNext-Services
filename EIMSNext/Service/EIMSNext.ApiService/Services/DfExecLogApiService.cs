using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	public class DfExecLogApiService(IResolver resolver) : ApiServiceBase<Df_ExecLog, DfExecLogViewModel, IDfExecLogService>(resolver)
	{
	}
}
