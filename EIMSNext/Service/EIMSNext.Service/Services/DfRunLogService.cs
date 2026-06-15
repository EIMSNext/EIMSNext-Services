using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class DfRunLogService(IResolver resolver) : MongoEntityServiceBase<Df_RunLog>(resolver), IDfRunLogService
    {
        protected override bool LogAudit => false;
    }
}
