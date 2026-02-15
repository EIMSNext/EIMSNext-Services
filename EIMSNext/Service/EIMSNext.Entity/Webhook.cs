using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// WebHook配置
    /// </summary>
    public class Webhook : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = "";

        /// <summary>
        /// 推送地址
        /// </summary>
        public string Url { get; set; } = "";
        /// <summary>
        /// Secret
        /// </summary>
        public string Secret { get; set; } = "";

        /// <summary>
        /// WebHook触发模式，类型为WebHookTrigger
        /// </summary>
        public long Triggers { get; set; }
    }

    /// <summary>
    /// WebHook触发模式
    /// </summary>
    [Flags]
    public enum WebHookTrigger
    {
        /// <summary>
        /// 未设置
        /// </summary>
        NotSet = 0,
        /// <summary>
        /// 数据新增
        /// </summary>
        DataCreated = 1 << 0,
        /// <summary>
        /// 数据修改
        /// </summary>
        DataUpdated = 1 << 1,
        /// <summary>
        /// 数据删除
        /// </summary>
        DateRemoved = 1 << 2,
        /// <summary>
        /// 流程状态变更
        /// </summary>
        WfStatusUpdated = 1 << 3,
        /// <summary>
        /// 流程待办变更
        /// </summary>
        WfTodoUpdated = 1 << 4,
    }
}
