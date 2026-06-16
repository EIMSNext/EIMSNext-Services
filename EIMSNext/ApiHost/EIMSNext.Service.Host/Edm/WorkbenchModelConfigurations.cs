using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;

namespace EIMSNext.Service.Host.Edm
{
    public class WorkbenchConfigModelConfiguration : CorpModelConfigurationBase<WorkbenchConfigViewModel, WorkbenchConfigRequest>
    {
    }

    public class WorkbenchFavoriteModelConfiguration : CorpModelConfigurationBase<WorkbenchFavoriteViewModel, WorkbenchFavoriteRequest>
    {
    }

    public class WorkbenchRecentVisitModelConfiguration : CorpModelConfigurationBase<WorkbenchRecentVisitViewModel, WorkbenchRecentVisitRequest>
    {
    }
}
