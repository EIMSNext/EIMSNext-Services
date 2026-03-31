using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
    public class AuthGroupService(IResolver resolver) : EntityServiceBase<AuthGroup>(resolver), IAuthGroupService
    {
        protected override bool LogicDelete => false;
    }
}
