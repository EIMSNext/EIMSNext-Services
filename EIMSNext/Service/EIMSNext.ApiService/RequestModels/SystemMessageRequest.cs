using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 系统消息请求
    /// </summary>
    public class SystemMessageRequest : RequestBase
    {
        /// <summary>
        /// 关联的FormNotifyId
        /// </summary>
        public string NotifyId { get; set; } = string.Empty;
        /// <summary>
        /// 消息标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 消息详情
        /// </summary>
        public string Detail { get; set; } = string.Empty;
        /// <summary>
        /// 链接地址
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// 消息接收人，员工ID
        /// </summary>
        public string ReceiverEmpId { get; set; } = string.Empty;
        /// <summary>
        /// 接收人姓名
        /// </summary>
        public string ReceiverName { get; set; } = string.Empty;
        /// <summary>
        /// 是否已读
        /// </summary>
        public bool IsRead { get; set; }
        /// <summary>
        /// 读取时间
        /// </summary>
        public long? ReadTime { get; set; }
        /// <summary>
        /// 过期时间
        /// </summary>
        public long ExpireTime { get; set; }
        /// <summary>
        /// 消息管道
        /// </summary>
        public FormNotifyChannel Channel { get; set; } = FormNotifyChannel.System;
        /// <summary>
        /// 消息分类
        /// </summary>
        public MessageCategory Category { get; set; } = MessageCategory.DataNotify;
    }
}
