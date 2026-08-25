using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 数据流HTTP触发样例抓取记录。
    /// </summary>
    public class EventFlowHookSample : CorpEntityBase
    {
        /// <summary>
        /// 数据流定义ID。
        /// </summary>
        public string EventFlowId { get; set; } = string.Empty;

        /// <summary>
        /// 应用ID。
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 客户端IP。
        /// </summary>
        public string ClientIp { get; set; } = string.Empty;

        /// <summary>
        /// 原始请求JSON。
        /// </summary>
        public string RawJson { get; set; } = string.Empty;

        /// <summary>
        /// 扁平化字段JSON。
        /// </summary>
        public string FlattenedFieldsJson { get; set; } = string.Empty;

        /// <summary>
        /// 样例抓取时间。
        /// </summary>
        public long CapturedAt { get; set; }
    }
}
