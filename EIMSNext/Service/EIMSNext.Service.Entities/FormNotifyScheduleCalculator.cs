using System.Text.Json;
using EIMSNext.Common.Extensions;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Entities
{
    public static class FormNotifyScheduleCalculator
    {
        public static long? CalculateNextTriggerTime(FormNotify notify, long anchorTime, long? afterTime = null)
        {
            if (notify.RepeatType == null)
            {
                return null;
            }

            var start = anchorTime.ToDateTimeMs();
            var cursor = afterTime.HasValue && afterTime.Value > anchorTime
                ? afterTime.Value.ToDateTimeMs()
                : start;

            DateTime? next = notify.RepeatType.Value switch
            {
                FormNotifyRepeatType.Once => afterTime.HasValue && afterTime.Value >= anchorTime ? null : start,
                FormNotifyRepeatType.Daily => NextDaily(start, cursor, 1),
                FormNotifyRepeatType.Weekly => NextWeekly(start, cursor, 1),
                FormNotifyRepeatType.BiWeekly => NextWeekly(start, cursor, 2),
                FormNotifyRepeatType.Monthly => NextMonthly(start, cursor, 1),
                FormNotifyRepeatType.Yearly => NextYearly(start, cursor, 1),
                FormNotifyRepeatType.Custom => NextCustom(start, cursor, notify.RepeatConfig),
                _ => null
            };

            if (next == null)
            {
                return null;
            }

            var nextMs = DateTime.SpecifyKind(next.Value, DateTimeKind.Utc).ToTimeStampMs();
            if (notify.EndTime.HasValue && nextMs > notify.EndTime.Value)
            {
                return null;
            }

            return nextMs;
        }

        public static bool ContainsFieldTokens(string? text)
        {
            return !string.IsNullOrWhiteSpace(text) && text.Contains("{{", StringComparison.Ordinal);
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

        private static RepeatConfig? ParseConfig(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<RepeatConfig>(json);
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
