using System.Text.Json.Serialization;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 数据流触发类型。
    /// </summary>
    public enum EventFlowTriggerKind
    {
        /// <summary>
        /// 表单触发。
        /// </summary>
        Form = 0,
        /// <summary>
        /// 定时触发。
        /// </summary>
        Schedule = 1,
        /// <summary>
        /// HTTP触发。
        /// </summary>
        Http = 2,
    }

    /// <summary>
    /// 定时触发时间源类型。
    /// </summary>
    public enum EventFlowScheduleSourceType
    {
        /// <summary>
        /// 自定义时间。
        /// </summary>
        Custom = 0,
        /// <summary>
        /// 表单字段时间。
        /// </summary>
        FormField = 1,
    }

    /// <summary>
    /// 时间偏移方向。
    /// </summary>
    public enum TimerOffsetDirection
    {
        /// <summary>
        /// 之前。
        /// </summary>
        Before = 0,
        /// <summary>
        /// 当天/原时刻。
        /// </summary>
        At = 1,
        /// <summary>
        /// 之后。
        /// </summary>
        After = 2,
    }

    /// <summary>
    /// 时间偏移单位。
    /// </summary>
    public enum TimerOffsetUnit
    {
        /// <summary>
        /// 分钟。
        /// </summary>
        Minute = 0,
        /// <summary>
        /// 小时。
        /// </summary>
        Hour = 1,
        /// <summary>
        /// 天。
        /// </summary>
        Day = 2,
    }

    /// <summary>
    /// 数据流定时触发设置。
    /// </summary>
    public class EventFlowTimeTriggerSetting
    {
        /// <summary>
        /// 时间源类型。
        /// </summary>
        public EventFlowScheduleSourceType SourceType { get; set; }

        /// <summary>
        /// 自定义触发开始时间。
        /// </summary>
        public long? StartTime { get; set; }

        /// <summary>
        /// 结束触发时间。
        /// </summary>
        public long? EndTime { get; set; }

        /// <summary>
        /// 时间字段。
        /// </summary>
        public string? TimeField { get; set; }

        /// <summary>
        /// 时间字段格式。
        /// </summary>
        public string? FieldFormat { get; set; }

        /// <summary>
        /// 偏移方向。
        /// </summary>
        public TimerOffsetDirection Direction { get; set; } = TimerOffsetDirection.At;

        /// <summary>
        /// 不带分钟字段时补充的固定时间，格式HH:mm。
        /// </summary>
        public string? FixedTime { get; set; }

        /// <summary>
        /// 偏移值。
        /// </summary>
        public int? OffsetValue { get; set; }

        /// <summary>
        /// 偏移单位。
        /// </summary>
        public TimerOffsetUnit? OffsetUnit { get; set; }

        /// <summary>
        /// 重复类型。
        /// </summary>
        public TimerRepeatType? RepeatType { get; set; }

        /// <summary>
        /// 重复配置JSON。
        /// </summary>
        public string? RepeatConfig { get; set; }
    }

    /// <summary>
    /// 数据流HTTP样例字段。
    /// </summary>
    public class EventFlowHttpSampleField
    {
        /// <summary>
        /// 字段键。
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 字段名称。
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// 字段类型，仅支持text或number。
        /// </summary>
        public string Type { get; set; } = "text";

        /// <summary>
        /// 示例值。
        /// </summary>
        public string? SampleValue { get; set; }
    }

    /// <summary>
    /// 数据流HTTP触发设置。
    /// </summary>
    public class EventFlowHttpTriggerSetting
    {
        /// <summary>
        /// 允许触发的IP列表。
        /// </summary>
        public List<string>? AllowedIps { get; set; }

        /// <summary>
        /// 是否启用自定义返回内容。
        /// </summary>
        public bool ResponseEnabled { get; set; }

        /// <summary>
        /// 返回状态码。
        /// </summary>
        public int? ResponseStatusCode { get; set; }

        /// <summary>
        /// 返回内容类型。
        /// </summary>
        public string? ResponseContentType { get; set; }

        /// <summary>
        /// 返回内容。
        /// </summary>
        public string? ResponseBody { get; set; }

        /// <summary>
        /// 最近一次样例抓取时间。
        /// </summary>
        public long? SampleCapturedAt { get; set; }

        /// <summary>
        /// HTTP字段样例。
        /// </summary>
        public List<EventFlowHttpSampleField>? SampleFields { get; set; }
    }

    /// <summary>
    /// HTTP触发请求上下文。
    /// </summary>
    public class EventFlowHttpRequestContext
    {
        /// <summary>
        /// 客户端IP。
        /// </summary>
        public string ClientIp { get; set; } = string.Empty;

        /// <summary>
        /// 请求头对象。
        /// </summary>
        public Dictionary<string, object?> Header { get; set; } = [];

        /// <summary>
        /// 请求体对象。
        /// </summary>
        public Dictionary<string, object?> Body { get; set; } = [];

        /// <summary>
        /// 扁平化字段列表。
        /// </summary>
        public List<EventFlowHttpSampleField> Fields { get; set; } = [];

        /// <summary>
        /// 原始请求内容。
        /// </summary>
        public string RawJson { get; set; } = string.Empty;
    }
}
