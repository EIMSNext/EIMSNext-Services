using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
	public class EmployeeGroupCategoryService(IResolver resolver) : EntityServiceBase<EmployeeGroupCategory>(resolver), IEmployeeGroupCategoryService
	{
	}
}
