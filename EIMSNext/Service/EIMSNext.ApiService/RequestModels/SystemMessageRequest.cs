using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    public class SystemMessageRequest : RequestBase
    {
        public string NotifyId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ReceiverEmpId { get; set; } = string.Empty;
        public string ReceiverName { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public long? ReadTime { get; set; }
        public long ExpireTime { get; set; }
        public FormNotifyChannel Channel { get; set; } = FormNotifyChannel.System;
        public MessageCategory Category { get; set; } = MessageCategory.DataNotify;
    }
}
