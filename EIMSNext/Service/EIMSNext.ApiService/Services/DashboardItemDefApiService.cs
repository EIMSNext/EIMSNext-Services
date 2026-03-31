using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	public class DashboardItemDefApiService(IResolver resolver) : ApiServiceBase<DashboardItemDef, DashboardItemDefViewModel, IDashboardItemDefService>(resolver)
	{
	}
}
