using EIMSNext.Core.Entities;
using EIMSNext.Service.Entities;

namespace EIMSNext.Async.Abstractions.Messaging
{
    public class NotifyReceiver
    {
        public string EmpId { get; set; } = string.Empty;

        public string EmpName { get; set; } = string.Empty;
    }

    [Queue("formnotify-dispatch")]
    public class FormNotifyDispatchTaskArgs
    {
        public string CorpId { get; set; } = string.Empty;

        public string DataId { get; set; } = string.Empty;

        public FormNotifyTriggerMode TriggerMode { get; set; }

        public Operator? Operator { get; set; }

        public FormData NewData { get; set; } = null!;

        public FormData? OldData { get; set; }
    }

    [Queue("system-message")]
    public class SystemMessageTaskArgs
    {
        public string CorpId { get; set; } = string.Empty;

        public string NotifyId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public long ExpireTime { get; set; }

        public MessageCategory Category { get; set; } = MessageCategory.DataNotify;

        public List<NotifyReceiver> Receivers { get; set; } = new();
    }

    [Queue("email")]
    public class EmailNotifyTaskArgs
    {
        public string CorpId { get; set; } = string.Empty;

        public string NotifyId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public List<NotifyReceiver> Receivers { get; set; } = new();
    }
}
