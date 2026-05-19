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
        /// 表单内容
        /// </summary>
        public FormContent Content { get; set; } = new FormContent();

        /// <summary>
        /// 是否台账
        /// </summary>
        public bool IsLedger { get; set; }

        /// <summary>
        /// 是否流程表单
        /// </summary>
        public bool UsingWorkflow { get; set; }

        /// <summary>
        /// 表单设置
        /// </summary>
        public FormSettings FormSettings { get; set; } = new FormSettings();
    }
}
