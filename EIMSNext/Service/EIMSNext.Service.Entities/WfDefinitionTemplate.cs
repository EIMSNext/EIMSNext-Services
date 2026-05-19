using EIMSNext.Common;
using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 工作流模板
    /// </summary>
    public class WfDefinitionTemplate : EntityBase
    {
        /// <summary>
        /// 所属应用模板 ID。
        /// </summary>
        public string AppTemplateId { get; set; } = string.Empty;

        /// <summary>
        /// 工作流名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 流程类型。
        /// </summary>
        public FlowType FlowType { get; set; } = FlowType.Workflow;

        /// <summary>
        /// 关联的表单模板 ID。
        /// </summary>
        public string ExternalTemplateId { get; set; } = string.Empty;

        /// <summary>
        /// 工作流描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 工作流定义内容。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 工作流元数据。
        /// </summary>
        public WfMetadata Metadata { get; set; } = new WfMetadata();

        /// <summary>
        /// 事件来源类型。
        /// </summary>
        public EventSourceType EventSource { get; set; }

        /// <summary>
        /// 事件来源模板 ID。
        /// </summary>
        public string? SourceTemplateId { get; set; }

        /// <summary>
        /// 事件触发设置。
        /// </summary>
        public EventSetting? EventSetting { get; set; }

        /// <summary>
        /// 是否禁用。
        /// </summary>
        public bool Disabled { get; set; }
    }
}
