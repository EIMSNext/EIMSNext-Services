using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 工作流执行日志实体
    /// </summary>
    public class Wf_ExecLog : LogEntityBase
    {
        /// <summary>
        /// 工作流实例ID
        /// </summary>
        public string WfInstanceId { get; set; } = string.Empty;
        /// <summary>
        /// 业务数据ID
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 工作流节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;
        /// <summary>
        /// 执行人员ID
        /// </summary>
        public string EmpId { get; set; } = string.Empty;
        /// <summary>
        /// 执行是否成功
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrMsg { get; set; } = string.Empty;
        /// <summary>
        /// 执行时间（Unix 毫秒时间戳，节点执行完成时刻）
        /// </summary>
        public long ExecTime { get; set; }
        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get; set; } = string.Empty;
        /// <summary>
        /// 节点类型
        /// </summary>
        public WfNodeType NodeType { get; set; }
        /// <summary>
        /// 审批轮次
        /// </summary>
        public int Round { get; set; }
    }
}
