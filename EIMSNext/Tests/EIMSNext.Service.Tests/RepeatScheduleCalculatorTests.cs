using EIMSNext.Common.Extensions;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class RepeatScheduleCalculatorTests
    {
        // 固定锚点：2026-01-01 09:00:00 UTC
        private static readonly long AnchorMs = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc).ToTimeStampMs();

        private static long Ms(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
            => new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc).ToTimeStampMs();

        // ---- RepeatType 为空 ----

        [TestMethod]
        public void CalculateNext_NullParameter_ReturnsNull()
        {
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(null!);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void CalculateNext_NullRepeatType_ReturnsNull()
        {
            var parameter = TimeTriggerParameter.Of(null, null, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.IsNull(result);
        }

        // ---- Once ----

        [TestMethod]
        public void CalculateNext_Once_NoAfter_ReturnsAnchor()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Once, null, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(AnchorMs, result);
        }

        [TestMethod]
        public void CalculateNext_Once_AfterBeforeAnchor_ReturnsAnchor()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Once, null, null, AnchorMs, AnchorMs - 60_000);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(AnchorMs, result);
        }

        [TestMethod]
        public void CalculateNext_Once_AfterEqualAnchor_ReturnsNull()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Once, null, null, AnchorMs, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void CalculateNext_Once_AfterAfterAnchor_ReturnsNull()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Once, null, null, AnchorMs, AnchorMs + 60_000);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.IsNull(result);
        }

        // ---- Daily ----

        [TestMethod]
        public void CalculateNext_Daily_NoAfter_ReturnsAnchorPlusOneDay()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Daily, null, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(AnchorMs + 24L * 3600 * 1000, result);
        }

        [TestMethod]
        public void CalculateNext_Daily_AfterOneDay_ReturnsAnchorPlusTwoDays()
        {
            var after = AnchorMs + 24L * 3600 * 1000;
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Daily, null, null, AnchorMs, after);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(AnchorMs + 48L * 3600 * 1000, result);
        }

        // ---- Weekly / BiWeekly ----

        [TestMethod]
        public void CalculateNext_Weekly_ReturnsAnchorPlusSevenDays()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Weekly, null, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(AnchorMs + 7L * 24 * 3600 * 1000, result);
        }

        [TestMethod]
        public void CalculateNext_BiWeekly_ReturnsAnchorPlusFourteenDays()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.BiWeekly, null, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(AnchorMs + 14L * 24 * 3600 * 1000, result);
        }

        // ---- Monthly / Yearly ----

        [TestMethod]
        public void CalculateNext_Monthly_NoAfter_ReturnsNextMonth()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Monthly, null, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(Ms(2026, 2, 1, 9, 0, 0), result);
        }

        [TestMethod]
        public void CalculateNext_Yearly_NoAfter_ReturnsNextYear()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Yearly, null, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(Ms(2027, 1, 1, 9, 0, 0), result);
        }

        // ---- EndTime ----

        [TestMethod]
        public void CalculateNext_Daily_ExceedsEndTime_ReturnsNull()
        {
            // Daily next = anchor + 1 day; endTime = anchor -> next > endTime -> null.
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Daily, null, AnchorMs, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void CalculateNext_Once_ExceedsEndTime_ReturnsNull()
        {
            var endTime = AnchorMs - 1; // 1ms before anchor
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Once, null, endTime, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.IsNull(result);
        }

        // ---- Custom Weekly ----

        [TestMethod]
        public void CalculateNext_CustomWeekly_SingleWeekday_NoAfter_FindsNextSameWeekday()
        {
            // 2026-01-01 is Thursday (DayOfWeek=4)
            var config = "{\"mode\":\"weekly\",\"interval\":1,\"weekdays\":[4]}";
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Custom, config, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            // next Thursday after 2026-01-01 is 2026-01-08
            Assert.AreEqual(Ms(2026, 1, 8, 9, 0, 0), result);
        }

        [TestMethod]
        public void CalculateNext_CustomWeekly_MultipleWeekdays_IntervalTwo_PicksEarliestAfterCursor()
        {
            // 2026-01-01 is Thursday (DayOfWeek=4)
            // weekdays: Mon(1) + Fri(5), interval 2 weeks
            // baseWeek step 0 = 2025-12-28 (Sunday).
            //   Mon(1) -> 2025-12-29 < anchor 2026-01-01, skip
            //   Fri(5) -> 2026-01-02 09:00 > cursor, return
            var config = "{\"mode\":\"weekly\",\"interval\":2,\"weekdays\":[1,5]}";
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Custom, config, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(Ms(2026, 1, 2, 9, 0, 0), result);
        }

        [TestMethod]
        public void CalculateNext_CustomWeekly_NoWeekdays_DefaultsToAnchorWeekday()
        {
            var config = "{\"mode\":\"weekly\",\"interval\":1}";
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Custom, config, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            // anchor weekday = Thursday, so next Thursday = 2026-01-08
            Assert.AreEqual(Ms(2026, 1, 8, 9, 0, 0), result);
        }

        [TestMethod]
        public void CalculateNext_CustomWeekly_EmptyConfig_ReturnsNull()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Custom, "", null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void CalculateNext_CustomWeekly_InvalidJson_ReturnsNull()
        {
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Custom, "{not-json", null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.IsNull(result);
        }

        // ---- Custom Monthly ----

        [TestMethod]
        public void CalculateNext_CustomMonthly_DayOfMonth_15()
        {
            // 2026-01-01 anchor → first month that is strictly after cursor
            // base Jan starts at 2026-01-01 09:00 (anchor). candidate=2026-01-15 09:00 > anchor? Yes.
            var config = "{\"mode\":\"monthly\",\"interval\":1,\"monthDay\":15}";
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Custom, config, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(Ms(2026, 1, 15, 9, 0, 0), result);
        }

        [TestMethod]
        public void CalculateNext_CustomMonthly_Relative_FirstMonday()
        {
            // 2026-01-01 is Thursday. weekIndex=1, weekday=1.
            // step 0 monthBase = 2026-01-01; ResolveNthWeekday(2026, 1, 1, 1, 09:00)
            //   firstDay 2026-01-01 (DayOfWeek=4 Thu); offset=(1-4+7)%7=4; day=1+4+0=5
            //   returns 2026-01-05 09:00 > cursor, so result is 2026-01-05.
            var config = "{\"mode\":\"monthly\",\"interval\":1,\"monthlyMode\":\"relative\",\"weekIndex\":1,\"weekday\":1}";
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Custom, config, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.AreEqual(Ms(2026, 1, 5, 9, 0, 0), result);
        }

        [TestMethod]
        public void CalculateNext_CustomMonthly_UnknownMode_ReturnsNull()
        {
            var config = "{\"mode\":\"yearly\"}";
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Custom, config, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void CalculateNext_CustomMonthly_DayClampedToMonthEnd()
        {
            // 31st clamped to Feb (28 days in 2026, not a leap year)
            var config = "{\"mode\":\"monthly\",\"interval\":1,\"monthDay\":31}";
            var parameter = TimeTriggerParameter.Of(TimerRepeatType.Custom, config, null, AnchorMs);
            var result = RepeatScheduleCalculator.CalculateNextTriggerTime(parameter);
            // step 0: 2026-01, day 31 -> 2026-01-31 09:00 > anchor. Returns that.
            Assert.AreEqual(Ms(2026, 1, 31, 9, 0, 0), result);
        }

        // ---- TimeTriggerParameter.Of ----

        [TestMethod]
        public void Of_Factory_BuildsAllFields()
        {
            var param = TimeTriggerParameter.Of(TimerRepeatType.Daily, "{}", 100, 200, 300);
            Assert.AreEqual(TimerRepeatType.Daily, param.RepeatType);
            Assert.AreEqual("{}", param.RepeatConfig);
            Assert.AreEqual(100L, param.EndTime);
            Assert.AreEqual(200L, param.AnchorTime);
            Assert.AreEqual(300L, param.AfterTime);
        }

        [TestMethod]
        public void Of_Factory_AfterTimeDefaultsToNull()
        {
            var param = TimeTriggerParameter.Of(TimerRepeatType.Weekly, null, null, 10);
            Assert.IsNull(param.AfterTime);
        }

        // ---- EventFlowTimeTriggerSettingExtensions.ToTimeTriggerParameter ----

        [TestMethod]
        public void ToTimeTriggerParameter_MapsAllFields()
        {
            var setting = new EventFlowTimeTriggerSetting
            {
                RepeatType = TimerRepeatType.Monthly,
                RepeatConfig = "{\"mode\":\"monthly\"}",
                EndTime = 999L,
            };
            var param = setting.ToTimeTriggerParameter(500L, 600L);
            Assert.AreEqual(TimerRepeatType.Monthly, param.RepeatType);
            Assert.AreEqual("{\"mode\":\"monthly\"}", param.RepeatConfig);
            Assert.AreEqual(999L, param.EndTime);
            Assert.AreEqual(500L, param.AnchorTime);
            Assert.AreEqual(600L, param.AfterTime);
        }
    }

    [TestClass]
    public class FormNotifyScheduleCalculatorTests
    {
        private static FormNotify MakeNotify(TimerRepeatType? repeat, string? repeatConfig = null, long? endTime = null)
        {
            return new FormNotify
            {
                Id = "n1",
                AppId = "a",
                FormId = "f",
                RepeatType = repeat,
                RepeatConfig = repeatConfig,
                EndTime = endTime,
            };
        }

        [TestMethod]
        public void CalculateNext_DelegatesToPrimitive_Once()
        {
            var notify = MakeNotify(TimerRepeatType.Once);
            long anchor = 1_000_000L;
            Assert.AreEqual(anchor, FormNotifyScheduleCalculator.CalculateNextTriggerTime(notify, anchor));
        }

        [TestMethod]
        public void CalculateNext_DelegatesToPrimitive_DailyRespectsAfterTime()
        {
            var notify = MakeNotify(TimerRepeatType.Daily);
            long anchor = 1_000_000L;
            long after = anchor + 24L * 3600 * 1000;
            Assert.AreEqual(anchor + 48L * 3600 * 1000, FormNotifyScheduleCalculator.CalculateNextTriggerTime(notify, anchor, after));
        }

        [TestMethod]
        public void CalculateNext_NullRepeatType_ReturnsNull()
        {
            var notify = MakeNotify(null);
            Assert.IsNull(FormNotifyScheduleCalculator.CalculateNextTriggerTime(notify, 1));
        }

        [TestMethod]
        public void ContainsFieldTokens_EmptyOrNull_ReturnsFalse()
        {
            Assert.IsFalse(FormNotifyScheduleCalculator.ContainsFieldTokens(null));
            Assert.IsFalse(FormNotifyScheduleCalculator.ContainsFieldTokens(""));
            Assert.IsFalse(FormNotifyScheduleCalculator.ContainsFieldTokens("纯文本"));
        }

        [TestMethod]
        public void ContainsFieldTokens_HasBraces_ReturnsTrue()
        {
            Assert.IsTrue(FormNotifyScheduleCalculator.ContainsFieldTokens("hello {{name}}"));
        }

        // ---- HasMinutePrecision ----

        [TestMethod]
        public void HasMinutePrecision_NullOrEmpty_ReturnsTrue()
        {
            Assert.IsTrue(FormNotifyScheduleCalculator.HasMinutePrecision(null));
            Assert.IsTrue(FormNotifyScheduleCalculator.HasMinutePrecision(""));
        }

        [TestMethod]
        public void HasMinutePrecision_FormatWithMm_ReturnsTrue()
        {
            Assert.IsTrue(FormNotifyScheduleCalculator.HasMinutePrecision("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(FormNotifyScheduleCalculator.HasMinutePrecision("yyyy-MM-dd HH:m"));
        }

        [TestMethod]
        public void HasMinutePrecision_DateOnlyFormat_ReturnsFalse()
        {
            Assert.IsFalse(FormNotifyScheduleCalculator.HasMinutePrecision("yyyy-MM-dd"));
        }

        // ---- ResolveFieldAnchor ----

        [TestMethod]
        public void ResolveFieldAnchor_WithMinutePrecision_ReturnsFieldValueAsIs()
        {
            var fieldValue = new DateTime(2026, 3, 5, 14, 30, 0, DateTimeKind.Utc);
            var anchor = FormNotifyScheduleCalculator.ResolveFieldAnchor(fieldValue, "yyyy-MM-dd HH:mm", "09:00");
            Assert.AreEqual(fieldValue, anchor);
        }

        [TestMethod]
        public void ResolveFieldAnchor_DateOnlyWithFixedTime_UsesFixedTime()
        {
            var fieldValue = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
            var anchor = FormNotifyScheduleCalculator.ResolveFieldAnchor(fieldValue, "yyyy-MM-dd", "15:45");
            Assert.AreEqual(new DateTime(2026, 3, 5, 15, 45, 0, DateTimeKind.Utc), anchor);
        }

        [TestMethod]
        public void ResolveFieldAnchor_DateOnlyWithoutFixedTime_DefaultsToNineAM()
        {
            var fieldValue = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
            var anchor = FormNotifyScheduleCalculator.ResolveFieldAnchor(fieldValue, "yyyy-MM-dd", null);
            Assert.AreEqual(new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc), anchor);
        }

        [TestMethod]
        public void ResolveFieldAnchor_DateOnlyInvalidFixedTime_DefaultsToNineAM()
        {
            var fieldValue = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
            var anchor = FormNotifyScheduleCalculator.ResolveFieldAnchor(fieldValue, "yyyy-MM-dd", "99:99");
            Assert.AreEqual(new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc), anchor);
        }

        // ---- ApplyOffset ----

        [TestMethod]
        public void ApplyOffset_DirectionAt_ReturnsAnchorUnchanged()
        {
            var anchor = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc);
            var result = FormNotifyScheduleCalculator.ApplyOffset(anchor, TimerOffsetDirection.At, 30, TimerOffsetUnit.Minute);
            Assert.AreEqual(anchor, result);
        }

        [TestMethod]
        public void ApplyOffset_BeforeMinutes_ShiftsBack()
        {
            var anchor = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc);
            var result = FormNotifyScheduleCalculator.ApplyOffset(anchor, TimerOffsetDirection.Before, 15, TimerOffsetUnit.Minute);
            Assert.AreEqual(anchor.AddMinutes(-15), result);
        }

        [TestMethod]
        public void ApplyOffset_AfterHours_ShiftsForward()
        {
            var anchor = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc);
            var result = FormNotifyScheduleCalculator.ApplyOffset(anchor, TimerOffsetDirection.After, 2, TimerOffsetUnit.Hour);
            Assert.AreEqual(anchor.AddHours(2), result);
        }

        [TestMethod]
        public void ApplyOffset_AfterDays_ShiftsForward()
        {
            var anchor = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc);
            var result = FormNotifyScheduleCalculator.ApplyOffset(anchor, TimerOffsetDirection.After, 3, TimerOffsetUnit.Day);
            Assert.AreEqual(anchor.AddDays(3), result);
        }

        [TestMethod]
        public void ApplyOffset_NullOffsetValue_ReturnsAnchorUnchanged()
        {
            var anchor = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc);
            var result = FormNotifyScheduleCalculator.ApplyOffset(anchor, TimerOffsetDirection.After, null, TimerOffsetUnit.Hour);
            Assert.AreEqual(anchor, result);
        }

        [TestMethod]
        public void ApplyOffset_ZeroOffsetValue_ReturnsAnchorUnchanged()
        {
            var anchor = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc);
            var result = FormNotifyScheduleCalculator.ApplyOffset(anchor, TimerOffsetDirection.After, 0, TimerOffsetUnit.Hour);
            Assert.AreEqual(anchor, result);
        }

        [TestMethod]
        public void ApplyOffset_NegativeOffsetValue_NormalizedToZero()
        {
            var anchor = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc);
            var result = FormNotifyScheduleCalculator.ApplyOffset(anchor, TimerOffsetDirection.Before, -5, TimerOffsetUnit.Minute);
            Assert.AreEqual(anchor, result);
        }

        // ---- ResolveAdjustedAnchor ----

        [TestMethod]
        public void ResolveAdjustedAnchor_DateOnlyWithOffset_ReturnsTimestamp()
        {
            var fieldValue = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
            var result = FormNotifyScheduleCalculator.ResolveAdjustedAnchor(
                fieldValue, "yyyy-MM-dd", "09:00",
                TimerOffsetDirection.After, 30, TimerOffsetUnit.Minute);
            var expected = new DateTime(2026, 3, 5, 9, 30, 0, DateTimeKind.Utc).ToTimeStampMs();
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ResolveAdjustedAnchor_WithMinutePrecision_StillAppliesOffset()
        {
            var fieldValue = new DateTime(2026, 3, 5, 14, 0, 0, DateTimeKind.Utc);
            var result = FormNotifyScheduleCalculator.ResolveAdjustedAnchor(
                fieldValue, "yyyy-MM-dd HH:mm", "09:00",
                TimerOffsetDirection.Before, 1, TimerOffsetUnit.Hour);
            var expected = new DateTime(2026, 3, 5, 13, 0, 0, DateTimeKind.Utc).ToTimeStampMs();
            Assert.AreEqual(expected, result);
        }
    }
}
