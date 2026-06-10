using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class FormDataChangeLogService(IResolver resolver) : EntityServiceBase<FormDataChangeLog>(resolver), IFormDataChangeLogService
    {
        protected override bool LogAudit => false;
    }
}
