using MongoDB.Driver;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence
{
    public class WorkflowPurger : IWorkflowPurger
    {
        private readonly IMongoDatabase _database;

        private IMongoCollection<WorkflowInstance> WorkflowInstances => _database.GetCollection<WorkflowInstance>(MongoPersistenceProvider.WorkflowCollectionName);

        public WorkflowPurger(IMongoDatabase database)
        {
            _database = database;
        }

        public async Task PurgeWorkflows(WorkflowStatus status, DateTime olderThan, CancellationToken cancellationToken = default)
        {
            var olderThanUtc = olderThan.ToUniversalTime();
            await WorkflowInstances.DeleteManyAsync(x => x.Status == status
                && x.CompleteTime < olderThanUtc, cancellationToken);
        }
    }
}