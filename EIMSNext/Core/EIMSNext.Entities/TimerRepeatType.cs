namespace EIMSNext.Entities
{
    /// <summary>
    /// 时间触发器的重复类型。
    /// 该枚举是 FormNotify（提醒助手）、EventFlowScheduleItem（智能助手）、WfExpireNotifyJob（流程超时）
    /// 三方共用的"定时器协议"，对应 <see cref="EIMSNext.Entities.RepeatScheduleCalculator"/>。
    /// 数值即持久化契约，成员一律显式赋值，新增成员只能追加在末尾。
    /// </summary>
    public enum TimerRepeatType
    {
        /// <summary>
        /// 只触发一次。
        /// </summary>
        Once = 0,
        /// <summary>
        /// 每天触发一次。
        /// </summary>
        Daily = 1,
        /// <summary>
        /// 每周触发一次。
        /// </summary>
        Weekly = 2,
        /// <summary>
        /// 每两周触发一次。
        /// </summary>
        BiWeekly = 3,
        /// <summary>
        /// 每月触发一次。
        /// </summary>
        Monthly = 4,
        /// <summary>
        /// 每年触发一次。
        /// </summary>
        Yearly = 5,
        /// <summary>
        /// 自定义重复规则。
        /// </summary>
        Custom = 6,
    }
}
