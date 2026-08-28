using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
    public class FormDataPermissionGroupService(IResolver resolver) : EntityServiceBase<FormDataPermissionGroup>(resolver), IFormDataPermissionGroupService
    {
        protected override bool LogicDelete => false;
    }
}
