using EIMSNext.Entities;

namespace EIMSNext.Async.Abstractions.Messaging
{
    [Queue("workflow-expire")]
    public class WorkflowExpireTaskArgs
    {
        public string CorpId { get; set; } = string.Empty;

        public string WfInstanceId { get; set; } = string.Empty;

        public string DataId { get; set; } = string.Empty;

        public string WfNodeId { get; set; } = string.Empty;

        public List<string> TaskIds { get; set; } = [];

        public WfExpireActionType ActionType { get; set; }
    }
}
