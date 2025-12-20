using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
	public class AdminGroupApiService(IResolver resolver) : ApiServiceBase<AdminGroup, AdminGroupViewModel, IAdminGroupService>(resolver)
	{
	}
}
