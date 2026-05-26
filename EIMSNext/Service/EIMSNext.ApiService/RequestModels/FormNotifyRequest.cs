using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 表单通知请求
    /// </summary>
    public class FormNotifyRequest : RequestBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// 提醒类型
        /// </summary>
        public FormNotifyTriggerMode TriggerMode { get; set; }
        /// <summary>
        /// 用于字段提醒的日期时间字段
        /// </summary>
        public string? TimeField { get; set; }
        /// <summary>
        /// 开始提醒时间
        /// </summary>
        public long? StartTime { get; set; }
        /// <summary>
        /// 结束提醒时间
        /// </summary>
        public long? EndTime { get; set; }
        /// <summary>
        /// 重复类型
        /// </summary>
        public FormNotifyRepeatType? RepeatType { get; set; }
        /// <summary>
        /// 重复配置(JSON)
        /// </summary>
        public string? RepeatConfig { get; set; }
        /// <summary>
        /// 数据变更后提醒时，触发提醒的字段
        /// </summary>
        public List<string>? ChangeFields { get; set; }
        /// <summary>
        /// 触发提醒的数据条件
        /// </summary>
        public string? DataFilter { get; set; }

        /// <summary>
        /// 提醒文字/消息标题
        /// </summary>
        public string? NotifyText { get; set; }
        /// <summary>
        /// 通知人
        /// </summary>
        public string? Notifiers { get; set; }
        /// <summary>
        /// 消息管道
        /// </summary>
        public long Channels { get; set; }
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }
}
