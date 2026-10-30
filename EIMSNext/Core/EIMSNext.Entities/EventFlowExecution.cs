using EIMSNext.Core.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// EventFlow 数据节点的幂等执行结果。
    /// </summary>
    public sealed class EventFlowNodeExecution : KeyedEntityBase
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

}
