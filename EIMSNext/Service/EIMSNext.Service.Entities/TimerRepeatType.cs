namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 时间触发器的重复类型，被表单提醒与数据流定时调度共用。
    /// </summary>
    public enum TimerRepeatType
    {
        /// <summary>
        /// 只触发一次。
        /// </summary>
        Once,
        /// <summary>
        /// 每天触发一次。
        /// </summary>
        Daily,
        /// <summary>
        /// 每周触发一次。
        /// </summary>
        Weekly,
        /// <summary>
        /// 每两周触发一次。
        /// </summary>
        BiWeekly,
        /// <summary>
        /// 每月触发一次。
        /// </summary>
        Monthly,
        /// <summary>
        /// 每年触发一次。
        /// </summary>
        Yearly,
        /// <summary>
        /// 自定义重复规则。
        /// </summary>
        Custom
    }
}
