using EIMSNext.Entity;

namespace EIMSNext.ApiService.RequestModel
{
    public class FormDefRequest : RequestBase
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
        /// 
        /// </summary>
        public FormType Type { get; set; } = FormType.Form;

        /// <summary>
        /// 
        /// </summary>
        public FormContent Content { get; set; } = new FormContent();

        /// <summary>
        /// 是否台账， 台账不支持手动增删改？？？
        /// </summary>
        public bool IsLedger { get; set; }

        /// <summary>
        /// 是否流程表单
        /// </summary>
        public bool UsingWorkflow { get; set; }
    }
}

