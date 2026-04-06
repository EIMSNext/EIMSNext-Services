using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    public class FormNotifyRequest : RequestBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// 提醒类型
        /// </summary>
        public FormNotifyTriggerMode TriggerMode { get; set; }
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
        public FormNotifyChannel Channels { get; set; }
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }
}
