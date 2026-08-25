using EIMSNext.Service.Entities;

namespace EIMSNext.Async.Abstractions.Messaging
{
    /// <summary>
    /// 数据流调度执行消息，由 Quartz 扫描作业发布，由 Async.Tasks 消费者调用 FlowApiClient.RunEventFlow 触发。
    /// </summary>
    [Queue("eventflow-run")]
    public class EventFlowRunTaskArgs
    {
        /// <summary>
        /// 企业ID。
        /// </summary>
        public string CorpId { get; set; } = string.Empty;

        /// <summary>
        /// 数据流定义ID。
        /// </summary>
        public string EventFlowId { get; set; } = string.Empty;

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
        /// 下游联级执行模式。
        /// </summary>
        public CascadeMode Cascade { get; set; } = CascadeMode.All;

        /// <summary>
        /// 触发节点ID（可选）。
        /// </summary>
        public string? WfNodeId { get; set; }
    }
}
