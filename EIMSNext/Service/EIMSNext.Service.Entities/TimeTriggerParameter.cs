namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 时间触发器的一次计算参数：repeat 配置（来自设置）+ 锚点与游标（来自调度项/调用方）。
    /// 故意不引用任何业务实体（FormNotify/Wf_Definition 等），保持独立可被多种触发场景共用。
    /// </summary>
    public record TimeTriggerParameter
    {
        /// <summary>
        /// 重复类型。
        /// </summary>
        public TimerRepeatType? RepeatType { get; init; }

        /// <summary>
        /// 重复配置（JSON 字符串，含 weekdays/monthlyMode 等）。
        /// </summary>
        public string? RepeatConfig { get; init; }

        /// <summary>
        /// 截止时间，超过该时间后停止计算下一次。
        /// </summary>
        public long? EndTime { get; init; }

        /// <summary>
        /// 触发锚点：重复推进的起点。
        /// </summary>
        public long AnchorTime { get; init; }

        /// <summary>
        /// 上一次触发时间；为空时按"未触发过"处理。
        /// </summary>
        public long? AfterTime { get; init; }

        /// <summary>
        /// 便捷工厂：5 个字段平铺传参。
        /// </summary>
        public static TimeTriggerParameter Of(
            TimerRepeatType? repeatType,
            string? repeatConfig,
            long? endTime,
            long anchorTime,
            long? afterTime = null)
        {
            return new TimeTriggerParameter
            {
                RepeatType = repeatType,
                RepeatConfig = repeatConfig,
                EndTime = endTime,
                AnchorTime = anchorTime,
                AfterTime = afterTime,
            };
        }
    }
}
