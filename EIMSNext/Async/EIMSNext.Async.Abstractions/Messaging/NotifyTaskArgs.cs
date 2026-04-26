using EIMSNext.Core.Entities;
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

        public string DataId { get; set; } = string.Empty;

        public string? TodoId { get; set; }

        public string? WfInstanceId { get; set; }

        public string? ApproveNodeId { get; set; }

        public FormNotifyTriggerMode? FormTriggerMode { get; set; }

        public Operator? Operator { get; set; }

        public FormData? NewData { get; set; }

        public FormData? OldData { get; set; }
    }

    public abstract class NotifyTaskArgsBase
    {

        public string CorpId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public MessageType MessageType { get; set; }

        public List<NotifyReceiver> Receivers { get; set; } = new();
    }

    [Queue("system-message")]
    public class SystemMessageTaskArgs : NotifyTaskArgsBase
    {
        public string NotifyId { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public long ExpireTime { get; set; }

        public MessageCategory Category { get; set; } = MessageCategory.DataNotify;

    }

    [Queue("email")]
    public class EmailNotifyTaskArgs : NotifyTaskArgsBase
    {
        public string NotifyId { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;
    }

    [Queue("webhook")]
    public class WebhookTaskArgs
    {
        public string CorpId { get; set; } = string.Empty;

        public string AppId { get; set; } = string.Empty;

        public string FormId { get; set; } = string.Empty;

        public WebHookTrigger Trigger { get; set; }

        public string PayloadJson { get; set; } = string.Empty;
    }
}
