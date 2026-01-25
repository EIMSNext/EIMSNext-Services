using EIMSNext.Entity;

namespace EIMSNext.ApiService.RequestModel
{
    public class WfDefinitionRequest : RequestBase
    {
        public string AppId { get; set; } = "";
        public string Name { get; set; } = string.Empty;
        public FlowType FlowType { get; set; } = FlowType.Workflow;
        public string ExternalId { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int Version { get; set; }
        public bool IsCurrent { get; set; }

        public string Content { get; set; } = string.Empty;
        public EventSourceType EventSource { get; set; }
        public string? SourceId { get; set; }
        public bool Disabled { get; set; }
    }
}

