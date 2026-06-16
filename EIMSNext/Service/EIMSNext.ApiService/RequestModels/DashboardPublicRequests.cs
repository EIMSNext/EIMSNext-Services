using EIMSNext.Core.Query;
using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    public class DashboardPublicDataRequest
    {
        public string ItemId { get; set; } = string.Empty;

        public DynamicFindOptions<FormData>? Options { get; set; }
    }

    public class DashboardPublicFilterOptionsRequest
    {
        public string ItemId { get; set; } = string.Empty;

        public FormDataFilterOptionsRequest Options { get; set; } = new();
    }

    public class DashboardPublicPayload
    {
        public DashboardDef Dashboard { get; set; } = null!;

        public List<DashboardItemDef> Items { get; set; } = [];
    }
}
