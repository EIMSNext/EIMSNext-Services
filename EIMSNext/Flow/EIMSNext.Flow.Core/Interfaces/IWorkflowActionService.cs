using EIMSNext.Service.Entities;
using EIMSNext.Core.Entities;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IWorkflowActionService
    {
        Task<WorkflowActionResult> WithdrawAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string formName, string comment);
        Task<WorkflowActionResult> UrgeAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string dataId);
        Task<WorkflowActionResult> TransferAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string targetEmployeeId, string comment);
        Task<WorkflowActionResult> AddSignAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string targetEmployeeId, string comment);
        Task<WorkflowActionResult> ReturnAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo, string targetNodeId, string comment);
        Task<List<ReturnTargetNodeResult>> GetReturnTargetsAsync(WorkflowActionDataContext context, WorkflowInstance workflowInstance, Wf_Todo todo);
        WorkflowActionStatusResult GetActionStatus(string currentEmployeeId, Wf_Todo? todo, Wf_Definition? definition);
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
