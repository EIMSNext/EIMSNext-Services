using EIMSNext.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// EventFlowTimeTriggerSetting 的扩展方法。
    /// </summary>
    public static class EventFlowTimeTriggerSettingExtensions
    {
        /// <summary>
        /// 把数据流定时触发设置 + 当次调用的锚点/游标包装为 <see cref="TimeTriggerParameter"/>。
        /// </summary>
        public static TimeTriggerParameter ToTimeTriggerParameter(
            this EventFlowTimeTriggerSetting setting,
            long anchorTime,
            long? afterTime = null)
        {
            return TimeTriggerParameter.Of(
                setting.RepeatType,
                setting.RepeatConfig,
                setting.EndTime,
                anchorTime,
                afterTime);
        }
    }
}
