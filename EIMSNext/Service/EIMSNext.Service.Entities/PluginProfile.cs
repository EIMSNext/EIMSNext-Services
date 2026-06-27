using EIMSNext.Core.Entities;
using EIMSNext.Plugin.Contracts;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 插件市场档案。
    /// 描述一个可被企业安装的插件及其市场展示信息。每个 (PluginId, Version) 组合对应一条 Profile。
    /// </summary>
    public class PluginProfile : EntityBase
    {
        /// <summary>插件唯一标识（同插件不同版本共享）。</summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>当前 Profile 对应的版本（语义化版本字符串）。</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>展示名称。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>一句话简介（用于列表/搜索结果）。</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>详细描述（Markdown）。</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>图标 URL。</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>详情页封面图 URL。</summary>
        public string CoverImage { get; set; } = string.Empty;

        /// <summary>横幅 Banner 图 URL（用于市场首页推广位）。</summary>
        public string BannerImage { get; set; } = string.Empty;

        /// <summary>详情页展示的轮播图 URL 列表。</summary>
        public List<string> GalleryImages { get; set; } = [];

        /// <summary>插件分类（如 "表单增强"）。</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>业务场景（如 "信息查询"）。</summary>
        public string Scenario { get; set; } = string.Empty;

        /// <summary>标签列表，用于市场搜索过滤。</summary>
        public List<string> Tags { get; set; } = [];

        /// <summary>开发者名称（展示用）。</summary>
        public string DeveloperName { get; set; } = string.Empty;

        /// <summary>开发者所在企业 ID（用于标识官方/合作方/独立开发者）。</summary>
        public string DeveloperCorpId { get; set; } = string.Empty;

        /// <summary>是否为官方插件。</summary>
        public bool IsOfficial { get; set; }

        /// <summary>是否标记为热门。</summary>
        public bool IsHot { get; set; }

        /// <summary>是否标记为推荐。</summary>
        public bool IsRecommended { get; set; }

        /// <summary>累计安装数（所有 corp、所有版本合计）。</summary>
        public long InstallCount { get; set; }

        /// <summary>在市场上的展示排序（值越小越靠前）。</summary>
        public int SortIndex { get; set; }

        /// <summary>发布状态，例如 <c>Draft</c> / <c>Published</c> / <c>Archived</c>。</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>首次发布时间。</summary>
        public DateTime? PublishedAt { get; set; }

        /// <summary>帮助文档 URL。</summary>
        public string HelpDocUrl { get; set; } = string.Empty;

        /// <summary>模板示例 URL（用于快速试用）。</summary>
        public string TemplateUrl { get; set; } = string.Empty;

        /// <summary>可选的定价方案（多个价格档位）。</summary>
        public List<PluginPricingPlan> PricingPlans { get; set; } = [];

        /// <summary>插件对外暴露的函数清单（含输入字段定义）。</summary>
        public List<PluginFunctionSnapshot> Functions { get; set; } = [];
    }

    /// <summary>
    /// 插件的一个定价档位。
    /// </summary>
    public class PluginPricingPlan
    {
        /// <summary>方案唯一 ID（同插件内唯一）。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>方案名称（"免费试用"/"月付" 等）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>价格（人民币元）。</summary>
        public decimal Price { get; set; }

        /// <summary>授权时长（天）。</summary>
        public int DurationDays { get; set; }

        /// <summary>时长单位（"天"/"月"/"年"，仅展示用）。</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>是否为试用档位（试用到期后不自动续费）。</summary>
        public bool IsTrial { get; set; }
    }

    /// <summary>
    /// 插件函数快照：描述一个可被工作流/Dataflow 调用的函数及其入参定义。
    /// </summary>
    public class PluginFunctionSnapshot
    {
        /// <summary>函数唯一 ID（插件内唯一）。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>函数名称。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>函数功能描述（Markdown）。</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>函数入参字段定义列表。</summary>
        public List<PluginFieldDesc> InputFields { get; set; } = [];
    }
}
