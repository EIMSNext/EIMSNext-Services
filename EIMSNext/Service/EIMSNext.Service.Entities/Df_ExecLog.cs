using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 执行日志实体，用于记录工作流节点执行情况
    /// </summary>
    public class Df_ExecLog : MongoEntityBase
    {
        /// <summary>
        /// 单次运行日志ID，新页面只读取带该字段的新日志。
        /// </summary>
        public string RunLogId { get; set; } = string.Empty;
        /// <summary>
        /// 企业ID
        /// </summary>
        public string CorpId { get; set; } = string.Empty;
        /// <summary>
        /// 数据流定义ID
        /// </summary>
        public string DataflowId { get; set; } = string.Empty;
        /// <summary>
        /// 工作流实例ID
        /// </summary>
        public string WfInstanceId { get; set; } = string.Empty;
        /// <summary>
        /// 数据ID
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;
        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get; set; } = string.Empty;
        /// <summary>
        /// 节点类型
        /// </summary>
        public WfNodeType NodeType { get; set; }
        /// <summary>
        /// 开始时间（毫秒）
        /// </summary>
        public long StartTime { get; set; }
        /// <summary>
        /// 结束时间（毫秒）
        /// </summary>
        public long? EndTime { get; set; }
        /// <summary>
        /// 执行是否成功
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrMsg { get; set; } = string.Empty;
        /// <summary>
        /// 失败原因
        /// </summary>
        public string FailureReason { get; set; } = string.Empty;
        /// <summary>
        /// 排查/修改建议
        /// </summary>
        public string TroubleshootingSuggestion { get; set; } = string.Empty;
        /// <summary>
        /// 执行摘要
        /// </summary>
        public string Summary { get; set; } = string.Empty;
        /// <summary>
        /// 执行时间（毫秒）
        /// </summary>
        public long ExecTime { get; set; }
    }
}
