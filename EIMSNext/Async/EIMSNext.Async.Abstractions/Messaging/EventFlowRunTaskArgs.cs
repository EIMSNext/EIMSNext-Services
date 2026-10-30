using EIMSNext.Entities;

namespace EIMSNext.Async.Abstractions.Messaging
{
    /// <summary>
    /// 数据流调度执行消息，由 Quartz 扫描作业发布，由 Async.Tasks 消费者调用 FlowApiClient.RunEventFlow 触发。
    /// </summary>
    [Queue("eventflow-run")]
    [OutboxQueue("eventflow-run")]
    public class EventFlowRunTaskArgs : IOutboxMessage
    {
        /// <summary>
        /// 企业ID。
        /// </summary>
        public string CorpId { get; set; } = string.Empty;

        /// <summary>
        /// 数据流定义ID。
        /// </summary>
        public string EventFlowId { get; set; } = string.Empty;

        /// <summary>明确允许匹配企业内全部表单 EventFlow；仅工作流异步触发使用。</summary>
        public bool MatchAllEventFlows { get; set; }

        /// <summary>本次定时触发的稳定执行 ID；消息重投必须沿用该值。</summary>
        public string ExecutionId { get; set; } = string.Empty;

        /// <summary>复用已有 EF 运行日志；非空时不新建 Ef_RunLog。</summary>
        public string? RunLogId { get; set; }

        /// <summary>人工重试的唯一投递尝试 ID；EF ExecutionId 保持原值用于节点幂等恢复。</summary>
        public string? RetryAttemptId { get; set; }

        /// <summary>人工指定的失败节点，用于后端校验重试授权。</summary>
        public string? RetryNodeId { get; set; }

        /// <summary>
        /// 应用ID。
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 表单ID。自定义定时可为空。
        /// </summary>
        public string? FormId { get; set; }

        /// <summary>
        /// 数据ID。自定义定时可为空。
        /// </summary>
        public string? DataId { get; set; }

        /// <summary>
        /// 事件来源。
        /// </summary>
        public EventSourceType EventSource { get; set; } = EventSourceType.Schedule;

        /// <summary>
        /// 事件类型。
        /// </summary>
        public EventType EventType { get; set; } = EventType.None;

        /// <summary>
        /// 触发节点ID（可选）。
        /// </summary>
        public string? WfNodeId { get; set; }

        /// <summary>审批节点流转匹配的动作，例如 submit/reject。</summary>
        public string? NodeAction { get; set; }

        /// <summary>触发 EF 的业务操作人。只传递展示/审计所需的身份信息，不传 AccessToken。</summary>
        public string? StarterId { get; set; }
        public string? StarterCode { get; set; }
        public string? StarterName { get; set; }

        /// <summary>EventFlow 链上下文 JSON。</summary>
        public string? ChainJson { get; set; }

        /// <summary>修改事件命中的字段，用于 EventFlow 字段触发条件。</summary>
        public List<string>? ChangeFields { get; set; }
    }
}
