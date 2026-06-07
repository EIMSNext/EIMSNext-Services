using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 仪表盘项模板
    /// </summary>
    public class DashboardItemTemplate : EntityBase
    {
        /// <summary>
        /// 所属应用模板 ID。
        /// </summary>
        public string AppTemplateId { get; set; } = string.Empty;

        /// <summary>
        /// 所属仪表盘模板 ID。
        /// </summary>
        public string DashboardTemplateId { get; set; } = string.Empty;

        /// <summary>
        /// 仪表盘项类型。
        /// </summary>
        public string ItemType { get; set; } = string.Empty;

        /// <summary>
        /// 布局块 ID。
        /// </summary>
        public string LayoutId { get; set; } = string.Empty;

        /// <summary>
        /// 仪表盘项名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 详细配置 JSON。
        /// </summary>
        public string Details { get; set; } = string.Empty;
    }
}
