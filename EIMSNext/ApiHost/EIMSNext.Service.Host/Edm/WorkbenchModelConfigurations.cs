using EIMSNext.Core.Entities;
using EIMSNext.Entities;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;

namespace EIMSNext.Service.Host.Edm
{
    public class WorkbenchConfigModelConfiguration : CorpModelConfigurationBase<WorkbenchConfig, WorkbenchConfigRequest>
    {
    }

    public class WorkbenchFavoriteModelConfiguration : CorpModelConfigurationBase<WorkbenchFavorite, WorkbenchFavoriteRequest>
    {
    }

    public class WorkbenchRecentVisitModelConfiguration : CorpModelConfigurationBase<WorkbenchRecentVisit, WorkbenchRecentVisitRequest>
    {
    }
}

