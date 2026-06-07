using EIMSNext.Core.Entities;
using EIMSNext.Plugin.Contracts;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 插件市场档案。
    /// </summary>
    public class PluginProfile : EntityBase
    {
        public string PluginId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string CoverImage { get; set; } = string.Empty;
        public string BannerImage { get; set; } = string.Empty;
        public List<string> GalleryImages { get; set; } = [];
        public string Category { get; set; } = string.Empty;
        public string Scenario { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = [];
        public string DeveloperName { get; set; } = string.Empty;
        public string DeveloperCorpId { get; set; } = string.Empty;
        public bool IsOfficial { get; set; }
        public bool IsHot { get; set; }
        public bool IsRecommended { get; set; }
        public long InstallCount { get; set; }
        public int SortIndex { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? PublishedAt { get; set; }
        public string HelpDocUrl { get; set; } = string.Empty;
        public string TemplateUrl { get; set; } = string.Empty;
        public List<PluginPricingPlan> PricingPlans { get; set; } = [];
        public List<PluginFunctionSnapshot> Functions { get; set; } = [];
    }

    public class PluginPricingPlan
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int DurationDays { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool IsTrial { get; set; }
    }

    public class PluginFunctionSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<PluginFieldDesc> InputFields { get; set; } = [];
    }
}
