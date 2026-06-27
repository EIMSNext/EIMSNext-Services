using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class WorkbenchConfigService(IResolver resolver) : EntityServiceBase<WorkbenchConfig>(resolver), IWorkbenchConfigService
    {
    }

    public class WorkbenchFavoriteService(IResolver resolver) : EntityServiceBase<WorkbenchFavorite>(resolver), IWorkbenchFavoriteService
    {
    }

    public class WorkbenchRecentVisitService(IResolver resolver) : EntityServiceBase<WorkbenchRecentVisit>(resolver), IWorkbenchRecentVisitService
    {
        protected override bool LogicDelete => false;
    }
}
