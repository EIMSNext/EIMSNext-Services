using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 打印模板
    /// </summary>
    public class PrintTemplate : CorpEntityBase
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

    /// <summary>
    /// 
    /// </summary>
    public enum PrintTemplateType
    {
        /// <summary>
        /// 
        /// </summary>
        Pdf
    }
}
