using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 应用模板
    /// </summary>
    public class AppTemplate : EntityBase
    {
        /// <summary>
        /// 模板名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 模板描述
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 模板图标
        /// </summary>
        public string Icon { get; set; } = "";
    }
}
