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
        /// 操作人
        /// </summary>
        public Operator? Operator { get; set; }
        /// <summary>
        /// 操作时间
        /// </summary>
        public long OperateTime { get; set; }
        /// <summary>
        /// 修改内容
        /// </summary>
        public List<DataUpdateContent> Content { get; set; } = new List<DataUpdateContent>();
    }
    /// <summary>
    /// 修改内容
    /// </summary>
    public class DataUpdateContent
    {
        /// <summary>
        /// 字段ID
        /// </summary>
        public required string FieldId { get; set; }
        /// <summary>
        /// 字段标题
        /// </summary>
        public required string FieldLabel { get; set; }
        /// <summary>
        /// 字段类型
        /// </summary>
        public required string FieldType { get; set; }
        /// <summary>
        /// 操作类型
        /// </summary>
        public DataChangeType ChangeType { get; set; }
        /// <summary>
        /// 旧值
        /// </summary>
        public object? OriVallue { get; set; }
        /// <summary>
        /// 新值
        /// </summary>
        public object? NewVallue { get; set; }
    }
}
