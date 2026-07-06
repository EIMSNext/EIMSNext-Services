using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单通知
    /// </summary>
    public class FormNotify : CorpEntityBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = "";

        /// <summary>
        /// 提醒目标类型。FormId 字段承载该目标类型对应的目标ID。
        /// </summary>
        public NotifyTargetType TargetType { get; set; } = NotifyTargetType.Form;

        /// <summary>
        /// 用于字段提醒的日期时间字段
        /// </summary>
        public string? TimeField { get; set; }

        /// <summary>
        /// 字段日期不含分钟时补的固定时间(HH:mm)
        /// </summary>
        public string? FixedTime { get; set; }

        /// <summary>
        /// 时间偏移方向
        /// </summary>
        public TimerOffsetDirection Direction { get; set; } = TimerOffsetDirection.At;

        /// <summary>
        /// 时间偏移量
        /// </summary>
        public int? OffsetValue { get; set; }

        /// <summary>
        /// 时间偏移单位
        /// </summary>
        public TimerOffsetUnit? OffsetUnit { get; set; }

        /// <summary>
        /// 字段日期格式, 仅用于决定是否需要补时
        /// </summary>
        public string? FieldFormat { get; set; }

        /// <summary>
        /// 开始提醒时间
        /// </summary>
        public long? StartTime { get; set; }

        /// <summary>
        /// 结束提醒时间
        /// </summary>
        public long? EndTime { get; set; }

        /// <summary>
        /// 重复类型
        /// </summary>
        public TimerRepeatType? RepeatType { get; set; }

        /// <summary>
        /// 重复配置(JSON)
        /// </summary>
        public string? RepeatConfig { get; set; }

        /// <summary>
        /// 下次触发时间
        /// </summary>
        public long? NextTriggerTime { get; set; }

        /// <summary>
        /// 上次触发时间
        /// </summary>
        public long? LastTriggerTime { get; set; }

        /// <summary>
        /// 调度版本号
        /// </summary>
        public long ScheduleVersion { get; set; }

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
        /// 解析后的公式， 用于内存过滤
        /// </summary>
        public string? DataExpressFilter { get; set; }

        /// <summary>
        /// 提醒文字/消息标题
        /// </summary>
        public string? NotifyText { get; set; }
        /// <summary>
        /// 通知人, ApprovalCandidate[]
        /// </summary>
        public string? Notifiers { get; set; }
        /// <summary>
        /// 消息管道 <see cref="NotifyChannel"/>
        /// </summary>
        public long Channels { get; set; }
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }

    /// <summary>
    /// 提醒目标类型
    /// </summary>
    public enum NotifyTargetType
    {
        /// <summary>
        /// 表单
        /// </summary>
        Form = 0,
        /// <summary>
        /// 仪表盘
        /// </summary>
        Dashboard = 1
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
    public enum NotifyChannel
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

    /// <summary>
    /// 消息分类
    /// </summary>
    public enum MessageCategory
    {
        /// <summary>
        /// 数据通知
        /// </summary>
        DataNotify,
        /// <summary>
        /// 应用通知
        /// </summary>
        AppNotify,
        /// <summary>
        /// 系统通知
        /// </summary>
        SystemNotify,
        /// <summary>
        /// 系统公告
        /// </summary>
        SystemNotice,
        /// <summary>
        /// 流程通知
        /// </summary>
        FlowNotify
    }

    /// <summary>
    /// 消息类型
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// 表单提醒
        /// </summary>
        FormNotify,
        /// <summary>
        /// 待办提醒
        /// </summary>
        WfTodoNotify,
        /// <summary>
        /// 待办超时提醒
        /// </summary>
        WfExpireNotify,
        /// <summary>
        /// 待办催办提醒
        /// </summary>
        WfUrgeNotify,
        /// <summary>
        /// 导出提醒
        /// </summary>
        ExportNotify,
        /// <summary>
        /// 导入提醒
        /// </summary>
        ImportNotify
    }
}
