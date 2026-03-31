using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
	public class DashboardItemDefService(IResolver resolver) : EntityServiceBase<DashboardItemDef>(resolver), IDashboardItemDefService
	{
	}
}
