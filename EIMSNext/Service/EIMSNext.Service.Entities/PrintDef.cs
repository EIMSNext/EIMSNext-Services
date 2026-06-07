using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 打印定义
    /// </summary>
    public class PrintDef : CorpEntityBase
    {
        /// <summary>
        /// 模板Id, 对于从模板安装的打印定义
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
        /// 打印定义名称。
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 设计器内容
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// 打印定义类型
        /// </summary>
        public PrintDefType PrintType { get; set; }
    }

    /// <summary>
    /// 打印定义类型。
    /// </summary>
    public enum PrintDefType
    {
        /// <summary>
        /// PDF 打印定义。
        /// </summary>
        Pdf
    }
}
