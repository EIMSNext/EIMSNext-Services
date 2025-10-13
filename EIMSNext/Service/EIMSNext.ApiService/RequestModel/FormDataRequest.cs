using System.Dynamic;
using EIMSNext.Core.Entity;

namespace EIMSNext.ApiService.RequestModel
{
    public class FormDataRequest : RequestBase
    {
        /// <summary>
        /// 0 - Save, 1 - Submit
        /// </summary>
        public DataAction Action { get; set; } = DataAction.SaveDraft;

        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// 表单内容
        /// </summary>
        public ExpandoObject Data { get; set; } = new ExpandoObject();
    }
}
