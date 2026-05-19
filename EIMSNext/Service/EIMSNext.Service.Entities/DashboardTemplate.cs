using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 仪表盘模板
    /// </summary>
    public class DashboardTemplate : EntityBase
    {
        /// <summary>
        /// 所属应用模板 ID。
        /// </summary>
        public string AppTemplateId { get; set; } = string.Empty;

        /// <summary>
        /// 仪表盘名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 仪表盘布局 JSON。
        /// </summary>
        public string Layout { get; set; } = string.Empty;
    }
}
