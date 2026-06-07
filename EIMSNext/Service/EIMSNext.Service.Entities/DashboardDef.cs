using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 自定义仪表盘
    /// </summary>
    public class DashboardDef : CorpEntityBase
    {
        /// <summary>
        /// 模板Id, 对于从模板安装的仪表盘
        /// </summary>
        public string? TemplateId { get; set; }

        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 仪表盘名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 布局
        /// </summary>
        public string Layout { get; set; } = string.Empty;
    }
}
