using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Extensions;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单数据
    /// </summary>
    public class FormData : DynamicEntity
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 流程状态
        /// </summary>
        public FlowStatus FlowStatus { get; set; }
    }
}
