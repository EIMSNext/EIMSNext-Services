using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单通知
    /// </summary>
    public class FormNotify : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = "";

        /// <summary>
        /// 提醒类型
        /// </summary>
        public FormNotifyTriggerMode TriggerMode { get; set; }
        /// <summary>
        /// 数据变更后提醒时，触发提醒的字段
        /// </summary>
        public List<string>? ChangeFields { get; set; }
        /// <summary>
        /// 触发提醒的数据条件
        /// </summary>
        public string? DataFilter { get; set; }
        /// <summary>
        /// 解析后的DataFilter, 用于数据库数据过滤
        /// </summary>
        public string? DataDynamicFilter { get; set; }

        /// <summary>
        /// 提醒文字/消息标题
        /// </summary>
        public string? NotifyText { get; set; }
        /// <summary>
        /// 通知人, ApprovalCandidate[]
        /// </summary>
        public string? Notifiers { get; set; }
        /// <summary>
        /// 消息管道
        /// </summary>
        public FormNotifyChannel Channels { get; set; }
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }

    /// <summary>
    /// 提醒类型
    /// </summary>
    public enum FormNotifyTriggerMode
    {
        /// <summary>
        /// 数据提交时
        /// </summary>
        DataAdded,
        /// <summary>
        /// 数据修改后
        /// </summary>
        DataChanged,
        /// <summary>
        /// 自定义
        /// </summary>
        CustomScheduled,
        /// <summary>
        /// 表单内时间字段
        /// </summary>
        TimeFieldScheduled
    }

    /// <summary>
    /// 通知管道
    /// </summary>
    [Flags]
    public enum FormNotifyChannel
    {
        /// <summary>
        /// 不发送
        /// </summary>
        None = 0,
        /// <summary>
        /// 站内消息
        /// </summary>
        System = 1 << 0,
        /// <summary>
        /// 邮件
        /// </summary>
        Email = 1 << 1,
    }
}
