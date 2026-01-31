using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence
{
    public interface IMongoPersistenceProvider : IPersistenceProvider
    {
        IQueryable<WorkflowInstance> GetWorkflowInstances();
        IQueryable<WorkflowInstance> GetWorkflowInstancesByReference(IEnumerable<string> references, WorkflowStatus? status);
        IQueryable<WorkflowInstance> GetWorkflowInstancesByDefId(IEnumerable<string> defIds, WorkflowStatus? status);
    }
}
