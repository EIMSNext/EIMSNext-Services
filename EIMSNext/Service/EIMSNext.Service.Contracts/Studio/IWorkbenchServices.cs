using EIMSNext.Core.Services;
using EIMSNext.Entities;
using MongoDB.Driver;

namespace EIMSNext.Service.Contracts
{
    public interface IWorkbenchConfigService : IService<WorkbenchConfig>
    {
    }

    public interface IWorkbenchFavoriteService : IService<WorkbenchFavorite>
    {
    }

    public interface IWorkbenchRecentVisitService : IService<WorkbenchRecentVisit>
    {
        Task<ReplaceOneResult> TouchRecentVisitAsync(WorkbenchRecentVisit entity);
    }
}
