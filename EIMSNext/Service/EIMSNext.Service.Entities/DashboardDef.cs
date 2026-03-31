using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 自定义仪表盘
    /// </summary>
    public class DashboardDef : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 布局
        /// </summary>
        public string Layout { get; set; } = string.Empty;
    }
}
