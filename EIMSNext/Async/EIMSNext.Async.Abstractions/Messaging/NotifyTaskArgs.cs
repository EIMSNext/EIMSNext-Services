using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Service.Entities;

namespace EIMSNext.Async.Abstractions.Messaging
{
    public class NotifyReceiver
    {
        public string EmpId { get; set; } = string.Empty;
        public string? Phone {  get; set; }
        public string? Email {  get; set; }

        public string EmpName { get; set; } = string.Empty;
    }

    [Queue("notify-dispatch")]
    public class NotifyDispatchTaskArgs
    {
        public string CorpId { get; set; } = string.Empty;

        public MessageType MessageType { get; set; }

        public string? AppId { get; set; }

        public string? FormId { get; set; }

        public NotifyTargetType TargetType { get; set; } = NotifyTargetType.Form;

        public string DataId { get; set; } = string.Empty;

        public string? TaskId { get; set; }

        public string? WfInstanceId { get; set; }

        public string? ApproveNodeId { get; set; }

        public FormNotifyTriggerMode? FormTriggerMode { get; set; }

        public Operator? Operator { get; set; }

        public FormData? NewData { get; set; }

        public FormData? OldData { get; set; }

        /// <summary>
        /// 事件产生时间戳（Unix 毫秒）。同一 DataId 的数据可能被多次修改/多次触发，
        /// 投递层幂等键据此区分不同的事件实例，防止后续事件被唯一索引去重吞掉。
        /// </summary>
        public long EventStamp { get; set; }
    }

    public abstract class NotifyTaskArgsBase
    {

        public string CorpId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public MessageType MessageType { get; set; }

        public List<NotifyReceiver> Receivers { get; set; } = new();

        /// <summary>
        /// 事件产生时间戳（Unix 毫秒）。同一通知配置/同一条数据可能被多次触发，
        /// 投递层幂等键据此区分不同的事件实例，防止后续触发被唯一索引去重吞掉。
        /// </summary>
        public long EventStamp { get; set; }
    }

    [Queue("system-message")]
    [OutboxQueue("system-message")]
    public class SystemMessageTaskArgs : NotifyTaskArgsBase, IOutboxMessage
    {
        public string NotifyId { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public long ExpireTime { get; set; }

        public MessageCategory Category { get; set; } = MessageCategory.DataNotify;

    }

    [Queue("email")]
    [OutboxQueue("email")]
    public class EmailNotifyTaskArgs : NotifyTaskArgsBase, IOutboxMessage
    {
        public EmailTaskType TaskType { get; set; }

        public string NotifyId { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;
    }

    public enum EmailTaskType
    {
        None,
        PlatWork,
    }

    [Queue("webhook")]
    [OutboxQueue("webhook")]
    public class WebhookTaskArgs : IOutboxMessage
    {
        public string CorpId { get; set; } = string.Empty;

        public string AppId { get; set; } = string.Empty;

        public string FormId { get; set; } = string.Empty;

        public string DataId { get; set; } = string.Empty;

        public WebHookTrigger Trigger { get; set; }

        public string PayloadJson { get; set; } = string.Empty;

        /// <summary>Stable event identifier supplied by the business event source.</summary>
        public string EventId { get; set; } = string.Empty;
    }
}
