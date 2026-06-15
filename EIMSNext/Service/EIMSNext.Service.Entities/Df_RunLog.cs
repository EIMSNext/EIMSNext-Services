using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 数据流单次运行日志。
    /// </summary>
    public class Df_RunLog : CorpEntityBase
    {
        public string AppId { get; set; } = string.Empty;
        public string DataflowId { get; set; } = string.Empty;
        public string DataflowName { get; set; } = string.Empty;
        public int DataflowVersion { get; set; }
        public string WfInstanceId { get; set; } = string.Empty;
        public DataflowTriggerKind TriggerKind { get; set; } = DataflowTriggerKind.Form;
        public EventSourceType EventSource { get; set; }
        public EventType EventType { get; set; }
        public Operator? TriggerBy { get; set; }
        public long TriggerTime { get; set; }
        public long StartTime { get; set; }
        public long? EndTime { get; set; }
        public bool Success { get; set; }
        public string ErrMsg { get; set; } = string.Empty;
    }
}
