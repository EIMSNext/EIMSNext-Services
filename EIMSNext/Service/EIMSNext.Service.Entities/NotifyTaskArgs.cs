using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 通知接收人
    /// </summary>
    public class NotifyReceiver
    {
        /// <summary>
        /// 员工ID
        /// </summary>
        public string EmpId { get; set; } = string.Empty;
        /// <summary>
        /// 员工姓名
        /// </summary>
        public string EmpName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表单通知分发任务参数
    /// </summary>
    public class FormNotifyDispatchTaskArgs
    {
        /// <summary>
        /// 企业ID
        /// </summary>
        public string CorpId { get; set; } = string.Empty;
        /// <summary>
        /// 数据ID
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 触发模式
        /// </summary>
        public FormNotifyTriggerMode TriggerMode { get; set; }
        /// <summary>
        /// 操作人
        /// </summary>
        public Operator? Operator { get; set; }
        /// <summary>
        /// 新数据
        /// </summary>
        public FormData NewData { get; set; } = null!;
        /// <summary>
        /// 旧数据
        /// </summary>
        public FormData? OldData { get; set; }
    }

    /// <summary>
    /// 系统消息任务参数
    /// </summary>
    public class SystemMessageTaskArgs
    {
        /// <summary>
        /// 企业ID
        /// </summary>
        public string CorpId { get; set; } = string.Empty;
        /// <summary>
        /// 通知ID
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
        /// 过期时间
        /// </summary>
        public long ExpireTime { get; set; }
        /// <summary>
        /// 消息分类
        /// </summary>
        public MessageCategory Category { get; set; } = MessageCategory.DataNotify;
        /// <summary>
        /// 接收人列表
        /// </summary>
        public List<NotifyReceiver> Receivers { get; set; } = new();
    }

    /// <summary>
    /// 邮件通知任务参数
    /// </summary>
    public class EmailNotifyTaskArgs
    {
        /// <summary>
        /// 企业ID
        /// </summary>
        public string CorpId { get; set; } = string.Empty;
        /// <summary>
        /// 通知ID
        /// </summary>
        public string NotifyId { get; set; } = string.Empty;
        /// <summary>
        /// 邮件标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 邮件内容
        /// </summary>
        public string Detail { get; set; } = string.Empty;
        /// <summary>
        /// 链接地址
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// 接收人列表
        /// </summary>
        public List<NotifyReceiver> Receivers { get; set; } = new();
    }

    /// <summary>
    /// 任务调用参数
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public class TaskInvokeArgs<T>
    {
        /// <summary>
        /// 方法名
        /// </summary>
        public string Method { get; set; } = string.Empty;
        /// <summary>
        /// 参数列表
        /// </summary>
        public List<T> Parameters { get; set; } = new();
    }
}
