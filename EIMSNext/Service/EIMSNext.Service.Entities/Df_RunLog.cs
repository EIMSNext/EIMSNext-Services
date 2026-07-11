using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 数据流单次运行日志。
    /// 每条记录对应一次 Dataflow 的实际执行（无论成功或失败）。
    /// </summary>
    public class Df_RunLog : CorpEntityBase
    {
        /// <summary>所属应用 ID。</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>数据流定义 ID。</summary>
        public string DataflowId { get; set; } = string.Empty;

        /// <summary>数据流名称（冗余字段，便于检索）。</summary>
        public string DataflowName { get; set; } = string.Empty;

        /// <summary>触发本次运行的 Dataflow 版本号。</summary>
        public int DataflowVersion { get; set; }

        /// <summary>关联的工作流实例 ID（Dataflow 内部启动了工作流时填写）。</summary>
        public string WfInstanceId { get; set; } = string.Empty;

        /// <summary>触发方式：表单事件、HTTP 触发、定时调度、手动等。</summary>
        public DataflowTriggerKind TriggerKind { get; set; } = DataflowTriggerKind.Form;

        /// <summary>事件来源类型（表单 / 工作流 / 外部 API 等）。</summary>
        public EventSourceType EventSource { get; set; }

        /// <summary>具体事件类型（与 <see cref="EventSource"/> 配合使用）。</summary>
        public EventType EventType { get; set; }

        /// <summary>触发人（系统任务时为 system）。</summary>
        public Operator? TriggerBy { get; set; }

        /// <summary>触发时间戳（毫秒）。</summary>
        public long TriggerTime { get; set; }

        /// <summary>实际开始执行时间戳（毫秒）。</summary>
        public long StartTime { get; set; }

        /// <summary>执行结束时间戳（毫秒）；未结束时为 null。</summary>
        public long? EndTime { get; set; }

        /// <summary>是否执行成功。</summary>
        public bool Success { get; set; }

        /// <summary>失败时的错误信息（成功时为空）。</summary>
        public string ErrMsg { get; set; } = string.Empty;
    }
}
