namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 时间触发器的重复类型。
    /// 该枚举是 FormNotify（提醒助手）、EventFlowScheduleItem（智能助手）、WfExpireNotifyJob（流程超时）
    /// 三方共用的"定时器协议"，对应 <see cref="EIMSNext.Service.Entities.RepeatScheduleCalculator"/>。
    /// 数值顺序必须保持稳定，已落库到 MongoDB，变更需做数据迁移。
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
