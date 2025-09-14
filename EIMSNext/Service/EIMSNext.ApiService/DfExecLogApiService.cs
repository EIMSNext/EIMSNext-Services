using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;

namespace EIMSNext.ApiService
{
	public class DfExecLogApiService(IResolver resolver) : ApiServiceBase<Df_ExecLog, DfExecLogViewModel>(resolver)
	{
	}
}
