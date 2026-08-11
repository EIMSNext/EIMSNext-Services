using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Abstractions.Extensions;
using EIMSNext.Core.Services.Extensions;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单数据修改日志
    /// </summary>
    public class FormDataChangeLog : CorpEntityBase
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
        /// 表单数据ID。
        /// 该字段是跨表追踪主键，可串联 AuditLog / FormDataChangeLog / Wf_TaskLog / Wf_ExecLog / Df_RunLogNode
        /// 五张表的同一业务记录事件。
        /// </summary>
        public string DataId { get; set; } = string.Empty;

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
        public List<DataChangeContent> Content { get; set; } = [];
    }

    /// <summary>
    /// 表单数据字段修改内容
    /// </summary>
    public class DataChangeContent
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
