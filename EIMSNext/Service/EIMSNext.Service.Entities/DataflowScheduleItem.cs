using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 数据流定时触发待执行项。
    /// </summary>
    public class DataflowScheduleItem : CorpEntityBase
    {
        /// <summary>
        /// 数据流定义ID。
        /// </summary>
        public string DataflowId { get; set; } = string.Empty;

        /// <summary>
        /// 应用ID。
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 表单ID。自定义定时触发可为空。
        /// </summary>
        public string? FormId { get; set; }

        /// <summary>
        /// 数据ID。自定义定时触发可为空。
        /// </summary>
        public string? DataId { get; set; }

        /// <summary>
        /// 计划触发时间。
        /// </summary>
        public long TriggerTime { get; set; }

        /// <summary>
        /// 计算触发的锚点时间。
        /// </summary>
        public long AnchorTime { get; set; }

        /// <summary>
        /// 调度版本号。
        /// </summary>
        public long ScheduleVersion { get; set; }

        /// <summary>
        /// 时间源类型。
        /// </summary>
        public DataflowScheduleSourceType SourceType { get; set; }
    }
}
