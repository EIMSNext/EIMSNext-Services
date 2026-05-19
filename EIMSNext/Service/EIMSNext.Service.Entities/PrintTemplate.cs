using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 打印模板
    /// </summary>
    public class PrintTemplate : CorpEntityBase
    {
        /// <summary>
        /// 模板Id, 对于从模板安装的打印模板
        /// </summary>
        public string? TemplateId { get; set; }

        /// <summary>
        /// 所属应用 ID。
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 关联表单 ID。
        /// </summary>
        public string FormId { get; set; } = "";
        /// <summary>
        /// 打印模板名称。
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
    /// 打印模板类型。
    /// </summary>
    public enum PrintTemplateType
    {
        /// <summary>
        /// PDF 打印模板。
        /// </summary>
        Pdf
    }
}
