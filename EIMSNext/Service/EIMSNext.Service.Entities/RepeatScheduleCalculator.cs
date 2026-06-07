using System.Text.Json;

using EIMSNext.Common.Extensions;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 重复型时间触发的下一次触发时间计算。
    /// 与具体业务实体（FormNotify/Wf_Definition）解耦，调用方通过 <see cref="TimeTriggerParameter"/> 传参。
    /// </summary>
    public static class RepeatScheduleCalculator
    {
        /// <summary>
        /// 根据 repeat 配置与锚点计算下一次触发时间。
        /// </summary>
        /// <param name="parameter">触发参数（repeat 类型/配置/截止/锚点/游标）。</param>
        /// <returns>下一次触发的毫秒时间戳；返回 null 表示不再触发（如 Once 已发过、超过 EndTime 等）。</returns>
        public static long? CalculateNextTriggerTime(TimeTriggerParameter parameter)
        {
            if (parameter?.RepeatType == null)
            {
                return null;
            }

            var start = parameter.AnchorTime.ToDateTimeMs();
            var cursor = parameter.AfterTime.HasValue && parameter.AfterTime.Value > parameter.AnchorTime
                ? parameter.AfterTime.Value.ToDateTimeMs()
                : start;

            DateTime? next = parameter.RepeatType.Value switch
            {
                TimerRepeatType.Once => parameter.AfterTime.HasValue && parameter.AfterTime.Value >= parameter.AnchorTime ? null : start,
                TimerRepeatType.Daily => NextDaily(start, cursor, 1),
                TimerRepeatType.Weekly => NextWeekly(start, cursor, 1),
                TimerRepeatType.BiWeekly => NextWeekly(start, cursor, 2),
                TimerRepeatType.Monthly => NextMonthly(start, cursor, 1),
                TimerRepeatType.Yearly => NextYearly(start, cursor, 1),
                TimerRepeatType.Custom => NextCustom(start, cursor, parameter.RepeatConfig),
                _ => null
            };

            if (next == null)
            {
                return null;
            }

            var nextMs = DateTime.SpecifyKind(next.Value, DateTimeKind.Utc).ToTimeStampMs();
            if (parameter.EndTime.HasValue && nextMs > parameter.EndTime.Value)
            {
                return null;
            }

            return nextMs;
        }

        private static DateTime? NextDaily(DateTime anchor, DateTime cursor, int days)
        {
            var next = anchor;
            while (next <= cursor)
            {
                next = next.AddDays(days);
            }

            return next;
        }

        private static DateTime? NextWeekly(DateTime anchor, DateTime cursor, int weeks)
        {
            var next = anchor;
            while (next <= cursor)
            {
                next = next.AddDays(7 * weeks);
            }

            return next;
        }

        private static DateTime? NextMonthly(DateTime anchor, DateTime cursor, int months)
        {
            var next = anchor;
            while (next <= cursor)
            {
                next = next.AddMonths(months);
            }

            return next;
        }

        private static DateTime? NextYearly(DateTime anchor, DateTime cursor, int years)
        {
            var next = anchor;
            while (next <= cursor)
            {
                next = next.AddYears(years);
            }

            return next;
        }

        private static DateTime? NextCustom(DateTime anchor, DateTime cursor, string? repeatConfig)
        {
            var config = ParseConfig(repeatConfig);
            if (config == null)
            {
                return null;
            }

            if (string.Equals(config.Mode, "weekly", StringComparison.OrdinalIgnoreCase))
            {
                return NextCustomWeekly(anchor, cursor, config);
            }

            if (string.Equals(config.Mode, "monthly", StringComparison.OrdinalIgnoreCase))
            {
                return NextCustomMonthly(anchor, cursor, config);
            }

            return null;
        }

        private static DateTime? NextCustomWeekly(DateTime anchor, DateTime cursor, RepeatConfig? config)
        {
            var interval = Math.Max(1, config?.Interval ?? 1);
            var weekdays = config?.Weekdays?.Distinct().Where(x => x >= 0 && x <= 6).OrderBy(x => x).ToList() ?? [];
            if (weekdays.Count == 0)
            {
                weekdays = [(int)anchor.DayOfWeek];
            }

            var weekStart = anchor.Date.AddDays(-(int)anchor.DayOfWeek);
            var timeOfDay = anchor.TimeOfDay;
            for (var step = 0; step < 520; step++)
            {
                var baseWeek = weekStart.AddDays(step * 7 * interval);
                foreach (var weekday in weekdays)
                {
                    var candidate = baseWeek.AddDays(weekday).Add(timeOfDay);
                    if (candidate < anchor)
                    {
                        continue;
                    }

                    if (candidate > cursor)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static DateTime? NextCustomMonthly(DateTime anchor, DateTime cursor, RepeatConfig? config)
        {
            var interval = Math.Max(1, config?.Interval ?? 1);
            for (var step = 0; step < 240; step++)
            {
                var monthBase = new DateTime(anchor.Year, anchor.Month, 1, anchor.Hour, anchor.Minute, anchor.Second, anchor.Millisecond, DateTimeKind.Utc).AddMonths(step * interval);
                var candidate = BuildMonthlyCandidate(anchor, monthBase, config);
                if (candidate < anchor)
                {
                    continue;
                }

                if (candidate > cursor)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static DateTime BuildMonthlyCandidate(DateTime anchor, DateTime monthBase, RepeatConfig? config)
        {
            if (string.Equals(config?.MonthlyMode, "relative", StringComparison.OrdinalIgnoreCase))
            {
                var weekIndex = Math.Max(1, config?.WeekIndex ?? GetWeekIndex(anchor));
                var weekday = config?.Weekday ?? (int)anchor.DayOfWeek;
                return ResolveNthWeekday(monthBase.Year, monthBase.Month, weekIndex, weekday, anchor.TimeOfDay);
            }

            var day = Math.Max(1, config?.MonthDay ?? anchor.Day);
            day = Math.Min(day, DateTime.DaysInMonth(monthBase.Year, monthBase.Month));
            return new DateTime(monthBase.Year, monthBase.Month, day, anchor.Hour, anchor.Minute, anchor.Second, anchor.Millisecond, DateTimeKind.Utc);
        }

        private static int GetWeekIndex(DateTime anchor)
        {
            return ((anchor.Day - 1) / 7) + 1;
        }

        private static DateTime ResolveNthWeekday(int year, int month, int weekIndex, int weekday, TimeSpan time)
        {
            var firstDay = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var offset = ((weekday - (int)firstDay.DayOfWeek) + 7) % 7;
            var day = 1 + offset + ((weekIndex - 1) * 7);
            var daysInMonth = DateTime.DaysInMonth(year, month);
            while (day > daysInMonth)
            {
                day -= 7;
            }

            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc).Add(time);
        }

        private static readonly JsonSerializerOptions ParseOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        private static RepeatConfig? ParseConfig(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<RepeatConfig>(json, ParseOptions);
            }
            catch
            {
                return null;
            }
        }

        private sealed class RepeatConfig
        {
            public string? Mode { get; set; }
            public int? Interval { get; set; }
            public List<int>? Weekdays { get; set; }
            public string? MonthlyMode { get; set; }
            public int? MonthDay { get; set; }
            public int? WeekIndex { get; set; }
            public int? Weekday { get; set; }
        }
    }
}
