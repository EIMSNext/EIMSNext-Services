using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 工作台配置视图模型。
    /// </summary>
    public class WorkbenchConfigViewModel : WorkbenchConfig
    {
    }

    /// <summary>
    /// 工作台收藏视图模型。
    /// </summary>
    public class WorkbenchFavoriteViewModel : WorkbenchFavorite
    {
    }

    /// <summary>
    /// 工作台最近访问视图模型。
    /// </summary>
    public class WorkbenchRecentVisitViewModel : WorkbenchRecentVisit
    {
    }

    /// <summary>
    /// 工作台目录应用视图模型。
    /// </summary>
    public class WorkbenchCatalogAppViewModel
    {
        /// <summary>应用 ID。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>应用名称。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>图标。</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>图标颜色。</summary>
        public string IconColor { get; set; } = string.Empty;

        /// <summary>菜单列表。</summary>
        public List<WorkbenchCatalogMenuViewModel> Menus { get; set; } = [];

        /// <summary>仪表盘列表。</summary>
        public List<WorkbenchCatalogDashboardViewModel> Dashboards { get; set; } = [];
    }

    /// <summary>
    /// 工作台目录菜单视图模型。
    /// </summary>
    public class WorkbenchCatalogMenuViewModel
    {
        /// <summary>菜单 ID。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>标题。</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>目标类型。</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>图标。</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>图标颜色。</summary>
        public string IconColor { get; set; } = string.Empty;

        /// <summary>子菜单列表。</summary>
        public List<WorkbenchCatalogMenuViewModel> Children { get; set; } = [];
    }

    /// <summary>
    /// 工作台目录仪表盘视图模型。
    /// </summary>
    public class WorkbenchCatalogDashboardViewModel
    {
        /// <summary>仪表盘 ID。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>仪表盘名称。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>应用 ID。</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>图表列表。</summary>
        public List<WorkbenchCatalogChartViewModel> Charts { get; set; } = [];
    }

    /// <summary>
    /// 工作台目录图表视图模型。
    /// </summary>
    public class WorkbenchCatalogChartViewModel
    {
        /// <summary>图表 ID。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>图表名称。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>仪表盘 ID。</summary>
        public string DashboardId { get; set; } = string.Empty;

        /// <summary>应用 ID。</summary>
        public string AppId { get; set; } = string.Empty;
    }
}
