using HKH.Mef2.Integration;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

namespace EIMSNext.Service
{
    public class AuthGroupService(IResolver resolver) : EntityServiceBase<AuthGroup>(resolver), IAuthGroupService
    {
        protected override bool LogicDelete => false;
    }
}
