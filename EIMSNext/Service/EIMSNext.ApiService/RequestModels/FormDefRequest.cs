using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 表单定义请求
    /// </summary>
    public class FormDefRequest : RequestBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 表单名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 表单内容
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
