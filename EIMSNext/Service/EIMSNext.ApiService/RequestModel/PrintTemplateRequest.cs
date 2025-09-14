using EIMSNext.Entity;

namespace EIMSNext.ApiService.RequestModel
{
    public class PrintTemplateRequest : RequestBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = "";
        /// <summary>
        /// 
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

