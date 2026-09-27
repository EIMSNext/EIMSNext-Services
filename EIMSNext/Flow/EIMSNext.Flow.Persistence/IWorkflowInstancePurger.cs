using WorkflowCore.Interface;

namespace EIMSNext.Flow.Persistence
{
    public interface IWorkflowInstancePurger : IWorkflowPurger
    {
        Task<IReadOnlyList<string>> DeleteWorkflowInstancesAsync(
            IEnumerable<string>? dataIds,
            IEnumerable<string>? workflowInstanceIds,
            CancellationToken cancellationToken = default);
    }
}
