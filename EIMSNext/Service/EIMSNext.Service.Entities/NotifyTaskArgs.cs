using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    public class NotifyReceiver
    {
        public string EmpId { get; set; } = string.Empty;
        public string EmpName { get; set; } = string.Empty;
    }

    public class FormNotifyDispatchTaskArgs
    {
        public string CorpId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public FormNotifyTriggerMode TriggerMode { get; set; }
        public Operator? Operator { get; set; }
        public FormData NewData { get; set; } = null!;
        public FormData? OldData { get; set; }
    }

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

    public class EmailNotifyTaskArgs
    {
        public string CorpId { get; set; } = string.Empty;
        public string NotifyId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public List<NotifyReceiver> Receivers { get; set; } = new();
    }

    public class TaskInvokeArgs<T>
    {
        public string Method { get; set; } = string.Empty;
        public List<T> Parameters { get; set; } = new();
    }
}
