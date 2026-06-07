using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单提醒待触发项。
    /// </summary>
    public class FormNotifyScheduleItem : CorpEntityBase
    {
        /// <summary>
        /// 提醒配置ID
        /// </summary>
        public string NotifyId { get; set; } = string.Empty;

        /// <summary>
        /// 数据ID，自定义提醒为空。
        /// </summary>
        public string? DataId { get; set; }

        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// 触发类型
        /// </summary>
        public FormNotifyTriggerMode TriggerMode { get; set; }

        /// <summary>
        /// 调度版本号
        /// </summary>
        public long ScheduleVersion { get; set; }

        /// <summary>
        /// 下一次触发时间
        /// </summary>
        public long TriggerTime { get; set; }

        /// <summary>
        /// 锚点时间。自定义提醒为开始时间，字段提醒为字段值。
        /// </summary>
        public long AnchorTime { get; set; }

        /// <summary>
        /// 时间字段，仅字段提醒使用。
        /// </summary>
        public string? TimeField { get; set; }
    }
}
