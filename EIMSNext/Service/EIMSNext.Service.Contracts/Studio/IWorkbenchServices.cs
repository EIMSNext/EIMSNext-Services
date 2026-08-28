using EIMSNext.Core.Services;
using EIMSNext.Entities;

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
    }
}
