using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 系统消息
    /// </summary>
    public class SystemMessage : CorpEntityBase
    {
        /// <summary>
        /// 关联的FormNotifyId
        /// </summary>
        public string? NotifyId { get; set; }
        /// <summary>
        /// 消息标题
        /// </summary>
        public string? Title { get; set; }
        /// <summary>
        /// 消息详情
        /// </summary>
        public string? Detail { get; set; }
        /// <summary>
        /// 链接地址
        /// </summary>
        public string? Url { get; set; }
        /// <summary>
        /// 消息接收人，员工ID
        /// </summary>
        public string? ReceiverEmpId { get; set; }
        /// <summary>
        /// 接收人姓名
        /// </summary>
        public string? ReceiverName { get; set; }
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
        /// 消息分类
        /// </summary>
        public MessageCategory Category { get; set; } = MessageCategory.DataNotify;
    }
}
