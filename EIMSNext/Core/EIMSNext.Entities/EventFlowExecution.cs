using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// EventFlow 数据节点的幂等执行结果。
    /// </summary>
    public sealed class EventFlowNodeExecution : MongoEntityBase
    {
        public string ExecutionKey { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public string WorkflowInstanceId { get; set; } = string.Empty;
        public string RunLogId { get; set; } = string.Empty;
        public string CorpId { get; set; } = string.Empty;
        public string EventFlowId { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string TargetKey { get; set; } = string.Empty;
        public int Ordinal { get; set; }
        public string? FormId { get; set; }
        public bool SingleResult { get; set; }
        public EventFlowNodeExecutionStatus Status { get; set; }
        public string ProcessingOwner { get; set; } = string.Empty;
        public long ProcessingStartedTime { get; set; }
        public long LeaseUntil { get; set; }
        public int AttemptCount { get; set; }
        public string ResultSnapshot { get; set; } = string.Empty;
        public long CompletedTime { get; set; }
    }

    public enum EventFlowNodeExecutionStatus
    {
        Processing = 0,
        Completed = 1,
        Failed = 2
    }

    /// <summary>
    /// 工作流节点流转触发的一批 EventFlow 的执行状态。
    /// </summary>
    public sealed class WorkflowTransitionExecution : MongoEntityBase
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string WorkflowInstanceId { get; set; } = string.Empty;
        public string CorpId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string NodeAction { get; set; } = string.Empty;
        public WorkflowTransitionStatus Status { get; set; }
        public string Error { get; set; } = string.Empty;
        public long CreateTime { get; set; }
        public long UpdateTime { get; set; }
    }

    public enum WorkflowTransitionStatus
    {
        Pending = 0,
        Running = 1,
        EventFlowsCompleted = 2,
        Failed = 3,
        Completed = 4
    }
}
