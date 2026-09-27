using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
    public class EmployeeGroupService(IResolver resolver) : EntityServiceBase<EmployeeGroup>(resolver), IEmployeeGroupService
    {      
    }
}
