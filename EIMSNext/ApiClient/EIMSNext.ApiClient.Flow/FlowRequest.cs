namespace EIMSNext.ApiClient.Flow
{
    public interface IFlowClient
    {
        Task<WfResponse?> Load(LoadDefRequest req, string accessToken);
        Task<WfResponse?> Start(StartRequest req, string accessToken);
        Task<WfResponse?> Approve(ApproveRequest req, string accessToken);
        Task<WfResponse?> Status(StatusRequest req, string accessToken);
        Task<WfResponse?> Terminate(TerminateRequest req, string accessToken);
        Task<WfResponse?> DeleteDef(DeleteRequest req, string accessToken);

        Task<WfResponse?> RunDataflow(DfRunRequest req, string accessToken);
    }

    public class LoadDefRequest()
    {
        public string WfDefinitionId { get; set; } = string.Empty;
        public int Version { get; set; }
    }
    public class StartRequest
    {
        public string WfDefinitionId { get; set; } = string.Empty;
        public int Version { get; set; }
        public string DataId { get; set; } = string.Empty;
        public CascadeMode DfCascade { get; set; }
        public string? EventIds { get; set; }
    }
    public class ApproveRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        //public string WorkerId { get; set; } = string.Empty;
        public ApproveAction Action { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
    public class StatusRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
    }
    public class DfRunRequest
    {
        public string DataId { get; set; } = string.Empty;
        public DfTrigger Trigger { get; set; }
        public CascadeMode DfCascade { get; set; }
        public string? EventIds { get; set; }
    }
    public enum DfTrigger
    {
        None = 0,
        Submit = 1,
        Update = 2,
        Delete = 4,
        Approving = 8,
        Approved = 16,
        Rejected = 32,
    }
    public enum ApproveAction
    {
        None,
        Approve,
        Reject,
        Return,
        AddSignPre,
        AddSignAfter
    }
    public enum CascadeMode
    {
        All,
        Specified,
        Never
    }
    public enum WorkflowStatus
    {
        Runnable,
        Suspended,
        Complete,
        Terminated
    }
    public class WfResponse
    {
        public string Id { get; set; } = string.Empty;
        public WorkflowStatus? Status { get; set; }
        public string? Error { get; set; }
    }

    public class TerminateRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WorkerId { get; set; } = string.Empty;
    }

    public class DeleteRequest()
    {
        public string? AppId { get; set; }
        public IEnumerable<string>? FormIds { get; set; }
        public bool? DeleteDef { get; set; }
    }
}
