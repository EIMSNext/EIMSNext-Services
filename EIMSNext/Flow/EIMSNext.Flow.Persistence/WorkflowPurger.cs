using Microsoft.EntityFrameworkCore;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence;

public sealed class WorkflowPurger(Func<CancellationToken, Task<IWfDbContext>> createContext) : IWorkflowInstancePurger
{
    public async Task PurgeWorkflows(WorkflowStatus status, DateTime olderThan, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        var cutoff = olderThan.ToUniversalTime();
        await db.WorkflowInstances.Where(x => x.Status == status && x.CompleteTime < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> DeleteWorkflowInstancesAsync(
        IEnumerable<string>? dataIds, IEnumerable<string>? workflowInstanceIds, CancellationToken cancellationToken = default)
    {
        var references = NormalizeIds(dataIds);
        var requestedIds = NormalizeIds(workflowInstanceIds);
        if (references.Length == 0 && requestedIds.Length == 0) return [];

        await using var db = await createContext(cancellationToken);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var query = db.WorkflowInstances.Where(x => references.Contains(x.Reference) || requestedIds.Contains(x.Id));
            var resolvedIds = (await query.Select(x => x.Id).ToListAsync(cancellationToken))
                .Concat(requestedIds).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            await query.ExecuteDeleteAsync(cancellationToken);
            if (resolvedIds.Length > 0)
                await db.EventSubscriptions.Where(x => resolvedIds.Contains(x.WorkflowId)).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (IReadOnlyList<string>)resolvedIds;
        });
    }

    private static string[] NormalizeIds(IEnumerable<string>? ids) => ids?
        .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
}
