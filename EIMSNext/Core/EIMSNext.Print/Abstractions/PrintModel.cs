using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Print.Abstractions
{
    public class PrintTemplate
    {
        /// <summary>
        /// 设计器内容
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// 打印模板类型
        /// </summary>
        public PrintType PrintType { get; set; }
    }
    public enum PrintType
    {
        Pdf,
        Excel,
        Word,
        Html
    }
    public class PrintOption
    {
        /// <summary>
        /// Html打印场景：link_form,link_data,link_query,flownode
        /// </summary>
        public string? HtmlPrintMode { get; set; }
    }
}
