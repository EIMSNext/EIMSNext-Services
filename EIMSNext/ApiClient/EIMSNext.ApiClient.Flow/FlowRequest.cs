namespace EIMSNext.ApiClient.Flow
{
    public interface IFlowClient
    {
        Task<WfResponse?> Load(LoadDefRequest req, string accessToken);
        Task<WfResponse?> Start(StartRequest req, string accessToken);
        Task<WfResponse?> Approve(ApproveRequest req, string accessToken);
        Task<WfResponse?> Submit(ApproveRequest req, string accessToken);
        Task<WfResponse?> Reject(ApproveRequest req, string accessToken);
        Task<WfResponse?> Return(ReturnRequest req, string accessToken);
        Task<WfResponse?> AddSign(AddSignRequest req, string accessToken);
        Task<WfResponse?> Transfer(TransferRequest req, string accessToken);
        Task<WfResponse?> Withdraw(WithdrawRequest req, string accessToken);
        Task<WfResponse?> Urge(UrgeRequest req, string accessToken);
        Task<WfActionStatusResponse?> ActionStatus(ActionStatusRequest req, string accessToken);
        Task<List<ReturnTargetNode>?> ReturnNodes(ActionStatusRequest req, string accessToken);
        Task<WfResponse?> Status(StatusRequest req, string accessToken);
        Task<WfResponse?> Terminate(TerminateRequest req, string accessToken);
        Task<WfResponse?> ChangeApprover(ChangeApproverRequest req, string accessToken);
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
        public string TargetNodeId { get; set; } = string.Empty;
        public string TargetEmployeeId { get; set; } = string.Empty;
    }
    public class ReturnRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }
    public class TransferRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetEmployeeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }
    public class AddSignRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetEmployeeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }
    public class WithdrawRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }
    public class StatusRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
    }
    public class UrgeRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
    }
    public class ActionStatusRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
    }
    public class WfActionStatusResponse
    {
        public bool CanWithdraw { get; set; }
        public bool CanUrge { get; set; }
        public string? Error { get; set; }
    }
    public class ReturnTargetNode
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public int Round { get; set; }
    }
    public class DfRunRequest
    {
        public string DataId { get; set; } = string.Empty;
        public EventSourceType EventSource { get; set; }
        public EventType EventType { get; set; }
        public CascadeMode DfCascade { get; set; }
        public string? EventIds { get; set; }
    }
    /// <summary>
    /// 事件来源类型枚举
    /// </summary>
    public enum EventSourceType
    {
        /// <summary>
        /// 无
        /// </summary>
        None,
        /// <summary>
        /// 表单
        /// </summary>
        Form,
        /// <summary>
        /// 按钮
        /// </summary>
        Buttton
    }
    public enum EventType
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
        AddSignAfter,
        AutoApprove,
        CopyTo,
        Withdraw,
        Transfer
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

    public class ChangeApproverRequest
    {
        public string WfInstanceId { get; set; } = string.Empty;
        public string DataId { get; set; } = string.Empty;
        public string WfNodeId { get; set; } = string.Empty;
        public string TargetEmployeeId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class DeleteRequest()
    {
        public string? AppId { get; set; }
        public IEnumerable<string>? FormIds { get; set; }
        public bool? DeleteDef { get; set; }
    }
}
