using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
	public class AdminGroupApiService(IResolver resolver) : ApiServiceBase<AdminGroup, AdminGroupViewModel, IAdminGroupService>(resolver)
	{
	}
}
