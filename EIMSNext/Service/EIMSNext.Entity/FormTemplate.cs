using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 表单模板
    /// </summary>
    public class FormTemplate : EntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public FormType Type { get; set; } = FormType.Form;
        /// <summary>
        /// 
        /// </summary>
        public string Icon { get; set; } = "";


        /// <summary>
        /// 
        /// </summary>
        public string AppTemplateId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string AppTemplateName { get; set; } = string.Empty;
    }
}
