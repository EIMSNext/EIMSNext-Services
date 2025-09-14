using System.Dynamic;

namespace EIMSNext.ApiService.RequestModel
{
    public class FormDataRequest : RequestBase
    {
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
