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

        /// <summary>
        /// 数据更新日志
        /// </summary>
        public List<DataUpdateLog> UpdateLog { get; set; } = new List<DataUpdateLog>();
    }
    /// <summary>
    /// 数据修改日志
    /// </summary>
    public class DataUpdateLog
    {
        /// <summary>
        /// 操作类型
        /// </summary>
        public OperateType OperateType { get; set; }
        /// <summary>
        /// 操作人
        /// </summary>
        public Operator? Operator { get; set; }
        /// <summary>
        /// 操作时间
        /// </summary>
        public long OperateTime { get; set; }
        /// <summary>
        /// 修改内容，Json
        /// </summary>
        public string? Content { get; set; }
    }
    /// <summary>
    /// 操作类型
    /// </summary>
    public enum OperateType
    {
        /// <summary>
        /// 
        /// </summary>
        Create = 0,
        /// <summary>
        /// 
        /// </summary>
        Modify,
        /// <summary>
        /// 
        /// </summary>
        Delete,
        /// <summary>
        /// 
        /// </summary>
        Restore
    }
}
