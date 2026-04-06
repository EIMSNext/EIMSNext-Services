using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单模板
    /// </summary>
    public class FormTemplate : EntityBase
    {
        /// <summary>
        /// 表单模板名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 表单类型
        /// </summary>
        public FormType Type { get; set; } = FormType.Form;
        /// <summary>
        /// 表单图标
        /// </summary>
        public string Icon { get; set; } = "";


        /// <summary>
        /// 关联的应用模板ID
        /// </summary>
        public string AppTemplateId { get; set; } = string.Empty;
        /// <summary>
        /// 关联的应用模板名称
        /// </summary>
        public string AppTemplateName { get; set; } = string.Empty;
    }
}
