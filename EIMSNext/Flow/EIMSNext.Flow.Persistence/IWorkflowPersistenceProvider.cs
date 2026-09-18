using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence
{
    public interface IWorkflowPersistenceProvider : IPersistenceProvider
    {
        Task ClearWorkflowRuntime(string workflowInstanceId, CancellationToken cancellationToken = default);
    }
}
