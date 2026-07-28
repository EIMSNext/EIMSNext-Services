using System.Dynamic;
using EIMSNext.Core.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 表单数据请求
    /// </summary>
    public class FormDataRequest : RequestBase
    {
        /// <summary>
        /// DataAction enum: 1 - Save, 2 - Submit
        /// </summary>
        public DataAction Action { get; set; } = DataAction.Save;

        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// 表单内容
        /// </summary>
        public ExpandoObject Data { get; set; } = new ExpandoObject();
    }
}
