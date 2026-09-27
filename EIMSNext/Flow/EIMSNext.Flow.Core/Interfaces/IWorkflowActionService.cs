using EIMSNext.Entities;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IWorkflowActionService
    {
        Task<WorkflowActionResult> WithdrawAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Task task, string formName, string comment);
        Task<WorkflowActionResult> UrgeAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Task task, string dataId);
        Task<WorkflowActionResult> TransferAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Task task, string targetEmployeeId, string comment);
        Task<WorkflowActionResult> AddSignAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Task task, string targetEmployeeId, string comment);
        Task<WorkflowActionResult> ChangeApproverAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Task task, string targetEmployeeId, string comment);
        Task<WorkflowActionResult> ReturnAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Task task, string targetNodeId, string comment);
        Task<List<ReturnTargetNodeResult>> GetReturnNodesAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Task task);
        WorkflowActionStatusResult GetActionStatus(string currentEmployeeId, Wf_Task? task, Wf_Definition? definition);
        Task ValidateSubmitConditionAsync(WorkflowInstance workflowInstance, Wf_Task task);
        Task ValidateNodeActionEnabledAsync(WorkflowInstance workflowInstance, Wf_Task task, NodeActionType actionType);
        Task<WorkflowActionResult> HandleExpiredTaskAsync(WorkflowInstance workflowInstance, Wf_Task task);
    }

    public class WorkflowActionDataContext
    {
        public string CorpId { get; set; } = string.Empty;
        public string CurrentEmployeeId { get; set; } = string.Empty;
        public Operator? CurrentEmployee { get; set; }
    }

    public class WorkflowActionResult
    {
        public string WorkflowInstanceId { get; set; } = string.Empty;
    }

    public class WorkflowActionStatusResult
    {
        public bool CanWithdraw { get; set; }
        public bool CanUrge { get; set; }
    }

    public class ReturnTargetNodeResult
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public int Round { get; set; }
    }
}
