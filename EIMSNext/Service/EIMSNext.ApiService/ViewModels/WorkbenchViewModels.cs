using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    public class WorkbenchConfigViewModel : WorkbenchConfig
    {
    }

    public class WorkbenchFavoriteViewModel : WorkbenchFavorite
    {
    }

    public class WorkbenchRecentVisitViewModel : WorkbenchRecentVisit
    {
    }

    public class WorkbenchCatalogAppViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public string IconColor { get; set; } = string.Empty;

        public List<WorkbenchCatalogMenuViewModel> Menus { get; set; } = [];

        public List<WorkbenchCatalogDashboardViewModel> Dashboards { get; set; } = [];
    }

    public class WorkbenchCatalogMenuViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string TargetType { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public string IconColor { get; set; } = string.Empty;

        public List<WorkbenchCatalogMenuViewModel> Children { get; set; } = [];
    }

    public class WorkbenchCatalogDashboardViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string AppId { get; set; } = string.Empty;

        public List<WorkbenchCatalogChartViewModel> Charts { get; set; } = [];
    }

    public class WorkbenchCatalogChartViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string DashboardId { get; set; } = string.Empty;

        public string AppId { get; set; } = string.Empty;
    }
}
