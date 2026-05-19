using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 打印模板模板
    /// </summary>
    public class PrintTemplateTemplate : EntityBase
    {
        /// <summary>
        /// 所属应用模板 ID。
        /// </summary>
        public string AppTemplateId { get; set; } = string.Empty;

        /// <summary>
        /// 关联的表单模板 ID。
        /// </summary>
        public string FormTemplateId { get; set; } = string.Empty;

        /// <summary>
        /// 打印模板名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 打印模板内容。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 打印模板类型。
        /// </summary>
        public PrintTemplateType PrintType { get; set; }
    }
}
