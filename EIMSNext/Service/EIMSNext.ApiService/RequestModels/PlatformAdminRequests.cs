using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    public class ECoinPriceBatchItemRequest
    {
        public ECoinTargetType TargetType { get; set; }
        public string? FeatureId { get; set; }
        public string? FeatureDesc { get; set; }
        public decimal Price { get; set; }
        public ECoinChargeType ChargeType { get; set; }
        public string? PluginId { get; set; }
    }

    public class PluginPublishRequest
    {
        public string PluginId { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? Icon { get; set; }
        public string? CoverImage { get; set; }
        public string? BannerImage { get; set; }
        public List<string>? GalleryImages { get; set; }
        public string? Category { get; set; }
        public string? Scenario { get; set; }
        public List<string>? Tags { get; set; }
        public string? DeveloperName { get; set; }
        public string? DeveloperCorpId { get; set; }
        public bool IsOfficial { get; set; }
        public bool IsHot { get; set; }
        public bool IsRecommended { get; set; }
        public int SortIndex { get; set; }
        public string? HelpDocUrl { get; set; }
        public string? TemplateUrl { get; set; }
    }
}
