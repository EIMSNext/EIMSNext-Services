using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 工作流定义请求
    /// </summary>
    public class WfDefinitionRequest : RequestBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 工作流名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 流程类型（工作流或数据流）
        /// </summary>
        public FlowType FlowType { get; set; } = FlowType.Workflow;
        /// <summary>
        /// 表单ID
        /// </summary>
        public string ExternalId { get; set; } = string.Empty;

        /// <summary>
        /// 工作流描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 工作流定义内容（JSON格式的流程配置）
        /// </summary>
        public string Content { get; set; } = string.Empty;
        /// <summary>
        /// 事件来源类型（表单或按钮）
        /// </summary>
        public EventSourceType EventSource { get; set; }
        /// <summary>
        /// 来源对象ID
        /// </summary>
        public string? SourceId { get; set; }
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }
}
