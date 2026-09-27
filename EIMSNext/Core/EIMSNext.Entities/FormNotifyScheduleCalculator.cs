using System.Text.RegularExpressions;

using EIMSNext.Common.Extensions;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 表单提醒调度计算器：
    /// 1) repeat 推进委托给 <see cref="RepeatScheduleCalculator"/>（业务实体无关 primitive）；
    /// 2) 字段锚点（补时/偏移）方法专属于"按字段时间"提醒场景；
    /// 3) 提醒文字字段占位符校验。
    /// </summary>
    public static class FormNotifyScheduleCalculator
    {
        private static readonly Regex HasMinuteRegex = new("([hH]{1,2}:mm)|([hH]{1,2}:m)", RegexOptions.Compiled);

        /// <summary>
        /// 基于 FormNotify 实体上的 repeat/StartTime 配置计算下一次触发时间。
        /// 内部把 FormNotify 字段折叠为 <see cref="TimeTriggerParameter"/> 后委派给 primitive。
        /// </summary>
        public static long? CalculateNextTriggerTime(FormNotify notify, long anchorTime, long? afterTime = null)
        {
            return RepeatScheduleCalculator.CalculateNextTriggerTime(
                TimeTriggerParameter.Of(notify.RepeatType, notify.RepeatConfig, notify.EndTime, anchorTime, afterTime));
        }

        /// <summary>
        /// 判断通知文案中是否含 {{...}} 形式的字段占位符。
        /// </summary>
        public static bool ContainsFieldTokens(string? text)
        {
            return !string.IsNullOrWhiteSpace(text) && text.Contains("{{", StringComparison.Ordinal);
        }

        /// <summary>
        /// 字段格式是否包含分钟精度（决定是否需要补时）。
        /// </summary>
        public static bool HasMinutePrecision(string? format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return true;
            }

            return HasMinuteRegex.IsMatch(format);
        }

        /// <summary>
        /// 按统一规则将字段值调整为触发锚点：含分钟直接使用；不含分钟使用字段日期 + FixedTime(HH:mm)。
        /// </summary>
        public static DateTime ResolveFieldAnchor(DateTime fieldValue, string? fieldFormat, string? fixedTime)
        {
            if (HasMinutePrecision(fieldFormat))
            {
                return DateTime.SpecifyKind(fieldValue, DateTimeKind.Utc);
            }

            if (TryParseFixedTime(fixedTime, out var hh, out var mm))
            {
                return new DateTime(fieldValue.Year, fieldValue.Month, fieldValue.Day, hh, mm, 0, DateTimeKind.Utc);
            }

            return new DateTime(fieldValue.Year, fieldValue.Month, fieldValue.Day, 9, 0, 0, DateTimeKind.Utc);
        }

        /// <summary>
        /// 对锚点时间应用方向+偏移量，得到首次/下次触发的基准时间。
        /// Direction=At 返回 anchor；Before/After 按单位前/后推 offset。
        /// </summary>
        public static DateTime ApplyOffset(DateTime anchor, TimerOffsetDirection direction, int? offsetValue, TimerOffsetUnit? offsetUnit)
        {
            if (direction == TimerOffsetDirection.At || !offsetValue.HasValue || offsetValue.Value == 0)
            {
                return anchor;
            }

            var value = Math.Max(0, offsetValue.Value);
            var span = offsetUnit switch
            {
                TimerOffsetUnit.Minute => TimeSpan.FromMinutes(value),
                TimerOffsetUnit.Hour => TimeSpan.FromHours(value),
                TimerOffsetUnit.Day => TimeSpan.FromDays(value),
                _ => TimeSpan.Zero,
            };

            return direction == TimerOffsetDirection.Before
                ? anchor - span
                : anchor + span;
        }

        /// <summary>
        /// 对字段值做补时+偏移后取毫秒时间戳。
        /// </summary>
        public static long ResolveAdjustedAnchor(DateTime fieldValue, string? fieldFormat, string? fixedTime, TimerOffsetDirection direction, int? offsetValue, TimerOffsetUnit? offsetUnit)
        {
            var anchor = ResolveFieldAnchor(fieldValue, fieldFormat, fixedTime);
            var adjusted = ApplyOffset(anchor, direction, offsetValue, offsetUnit);
            return DateTime.SpecifyKind(adjusted, DateTimeKind.Utc).ToTimeStampMs();
        }

        private static bool TryParseFixedTime(string? value, out int hour, out int minute)
        {
            hour = 0;
            minute = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            return int.TryParse(parts[0], out hour) && int.TryParse(parts[1], out minute)
                && hour >= 0 && hour < 24 && minute >= 0 && minute < 60;
        }
    }
}
