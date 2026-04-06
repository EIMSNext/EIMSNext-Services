using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 打印模板请求
    /// </summary>
    public class PrintTemplateRequest : RequestBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = "";
        /// <summary>
        /// 模板名称
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 设计器内容
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// 打印模板类型
        /// </summary>
        public PrintTemplateType PrintType { get; set; }
    }
}
