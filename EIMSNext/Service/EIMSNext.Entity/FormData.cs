using EIMSNext.Core;
using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 表单数据
    /// </summary>
    public class FormData : DynamicEntity
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
        /// 流程状态
        /// </summary>
        public FlowStatus FlowStatus { get; set; }
    }    
}
