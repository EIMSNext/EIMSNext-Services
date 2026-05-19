using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 应用中心档案
    /// </summary>
    public class AppProfile : EntityBase
    {
        /// <summary>
        /// 应用名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 列表页摘要。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 详细描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 应用图标。
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// 封面图片。
        /// </summary>
        public string CoverImage { get; set; } = string.Empty;

        /// <summary>
        /// 横幅图片。
        /// </summary>
        public string BannerImage { get; set; } = string.Empty;

        /// <summary>
        /// 详情页图库。
        /// </summary>
        public List<string> GalleryImages { get; set; } = [];

        /// <summary>
        /// 分类。
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// 行业。
        /// </summary>
        public string Industry { get; set; } = string.Empty;

        /// <summary>
        /// 标签。
        /// </summary>
        public List<string> Tags { get; set; } = [];

        /// <summary>
        /// 发布者。
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// 安装次数。
        /// </summary>
        public long InstallCount { get; set; }

        /// <summary>
        /// 排序索引。
        /// </summary>
        public int SortIndex { get; set; }

        /// <summary>
        /// 是否官方应用。
        /// </summary>
        public bool IsOfficial { get; set; }

        /// <summary>
        /// 是否热门应用。
        /// </summary>
        public bool IsHot { get; set; }

        /// <summary>
        /// 是否推荐应用。
        /// </summary>
        public bool IsRecommended { get; set; }

        /// <summary>
        /// 主题色。
        /// </summary>
        public string ThemeColor { get; set; } = string.Empty;

        /// <summary>
        /// 关联的应用模板 ID。
        /// </summary>
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>
        /// 发布状态。
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 发布时间。
        /// </summary>
        public DateTime? PublishedAt { get; set; }
    }
}
