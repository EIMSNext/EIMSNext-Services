using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单提醒发送日志，用于幂等去重。
    /// </summary>
    public class FormNotifyDispatchLog : CorpEntityBase
    {
        /// <summary>
        /// 提醒配置ID
        /// </summary>
        public string NotifyId { get; set; } = string.Empty;

        /// <summary>
        /// 数据ID，自定义提醒为空。
        /// </summary>
        public string? DataId { get; set; }

        /// <summary>
        /// 触发时间
        /// </summary>
        public long TriggerTime { get; set; }
    }
}
