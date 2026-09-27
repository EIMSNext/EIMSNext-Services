using MongoDB.Driver;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence
{
    public class WorkflowPurger : IWorkflowInstancePurger
    {
        private readonly IWfDbContext _dbContext;

        private IMongoCollection<WorkflowInstance> WorkflowInstances => _dbContext.WorkflowInstances;

        public WorkflowPurger(IWfDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task PurgeWorkflows(WorkflowStatus status, DateTime olderThan, CancellationToken cancellationToken = default)
        {
            var olderThanUtc = olderThan.ToUniversalTime();
            await WorkflowInstances.DeleteManyAsync(x => x.Status == status
                && x.CompleteTime < olderThanUtc, cancellationToken);
        }

        public async Task<IReadOnlyList<string>> DeleteWorkflowInstancesAsync(
            IEnumerable<string>? dataIds,
            IEnumerable<string>? workflowInstanceIds,
            CancellationToken cancellationToken = default)
        {
            var references = NormalizeIds(dataIds);
            var requestedIds = NormalizeIds(workflowInstanceIds);
            if (references.Count == 0 && requestedIds.Count == 0)
            {
                return [];
            }

            var workflowFilter = BuildWorkflowFilter(references, requestedIds);

            using var session = await _dbContext.StartSessionAsync(cancellationToken);
            session.StartTransaction();
            try
            {
                var resolvedIds = await WorkflowInstances
                    .Find(session, workflowFilter)
                    .Project(x => x.Id)
                    .ToListAsync(cancellationToken);
                resolvedIds = resolvedIds
                    .Concat(requestedIds)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                await WorkflowInstances.DeleteManyAsync(session, workflowFilter, cancellationToken: cancellationToken);
                if (resolvedIds.Count > 0)
                {
                    var subscriptionCollection = _dbContext.EventSubscriptions;
                    await subscriptionCollection.DeleteManyAsync(
                        session,
                        Builders<EventSubscription>.Filter.In(x => x.WorkflowId, resolvedIds),
                        cancellationToken: cancellationToken);
                }

                await session.CommitTransactionAsync(cancellationToken);
                return resolvedIds;
            }
            catch
            {
                await session.AbortTransactionAsync(cancellationToken);
                throw;
            }
        }

        private static FilterDefinition<WorkflowInstance> BuildWorkflowFilter(
            IReadOnlyCollection<string> references,
            IReadOnlyCollection<string> workflowInstanceIds)
        {
            var builder = Builders<WorkflowInstance>.Filter;
            var filters = new List<FilterDefinition<WorkflowInstance>>();
            if (references.Count > 0)
            {
                filters.Add(builder.In(x => x.Reference, references));
            }
            if (workflowInstanceIds.Count > 0)
            {
                filters.Add(builder.In(x => x.Id, workflowInstanceIds));
            }

            return filters.Count == 1 ? filters[0] : builder.Or(filters);
        }

        private static List<string> NormalizeIds(IEnumerable<string>? ids)
        {
            return ids?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }
    }
}
