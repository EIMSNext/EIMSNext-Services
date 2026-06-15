using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	public class DfRunLogNodeApiService(IResolver resolver) : ApiServiceBase<Df_RunLogNode, DfRunLogNodeViewModel, IDfRunLogNodeService>(resolver)
	{
	}
}
