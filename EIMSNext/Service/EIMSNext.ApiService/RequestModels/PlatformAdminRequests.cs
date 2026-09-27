using EIMSNext.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// E 币价格批量配置项请求。
    /// </summary>
    public class ECoinPriceBatchItemRequest
    {
        /// <summary>目标类型。</summary>
        public ECoinTargetType TargetType { get; set; }

        /// <summary>功能 ID。</summary>
        public string? FeatureId { get; set; }

        /// <summary>功能描述。</summary>
        public string? FeatureDesc { get; set; }

        /// <summary>价格。</summary>
        public decimal Price { get; set; }

        /// <summary>计费类型。</summary>
        public ECoinChargeType ChargeType { get; set; }

        /// <summary>插件 ID。</summary>
        public string? PluginId { get; set; }
    }

    /// <summary>
    /// 插件发布请求。
    /// </summary>
    public class PluginPublishRequest
    {
        /// <summary>插件 ID。</summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>摘要。</summary>
        public string? Summary { get; set; }

        /// <summary>图标。</summary>
        public string? Icon { get; set; }

        /// <summary>封面图。</summary>
        public string? CoverImage { get; set; }

        /// <summary>横幅图。</summary>
        public string? BannerImage { get; set; }

        /// <summary>画廊图片列表。</summary>
        public List<string>? GalleryImages { get; set; }

        /// <summary>分类。</summary>
        public string? Category { get; set; }

        /// <summary>场景。</summary>
        public string? Scenario { get; set; }

        /// <summary>标签列表。</summary>
        public List<string>? Tags { get; set; }

        /// <summary>开发者名称。</summary>
        public string? DeveloperName { get; set; }

        /// <summary>开发者企业 ID。</summary>
        public string? DeveloperCorpId { get; set; }

        /// <summary>是否官方。</summary>
        public bool IsOfficial { get; set; }

        /// <summary>是否热门。</summary>
        public bool IsHot { get; set; }

        /// <summary>是否推荐。</summary>
        public bool IsRecommended { get; set; }

        /// <summary>排序索引。</summary>
        public int SortIndex { get; set; }

        /// <summary>帮助文档地址。</summary>
        public string? HelpDocUrl { get; set; }

        /// <summary>模板地址。</summary>
        public string? TemplateUrl { get; set; }
    }
}
