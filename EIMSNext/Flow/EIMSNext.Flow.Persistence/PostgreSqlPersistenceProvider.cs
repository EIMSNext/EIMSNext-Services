using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence;

/// <summary>WorkflowCore storage using independent PostgreSQL contexts for concurrent workers.</summary>
public sealed class PostgreSqlPersistenceProvider(
    Func<CancellationToken, Task<IWfDbContext>> createContext,
    ILogger<PostgreSqlPersistenceProvider> logger) : IWorkflowPersistenceProvider
{
    public async Task<string> CreateNewWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync(cancellationToken);
        return workflow.Id;
    }

    public async Task PersistWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        db.WorkflowInstances.Update(workflow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken = default)
    {
        // One SaveChanges transaction persists both the instance and its subscriptions.
        await using var db = await createContext(cancellationToken);
        db.WorkflowInstances.Update(workflow);
        db.EventSubscriptions.AddRange(subscriptions ?? []);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        var ticks = asAt.ToUniversalTime().Ticks;
        return await db.WorkflowInstances.AsNoTracking()
            .Where(x => x.NextExecution.HasValue && x.NextExecution <= ticks && x.Status == WorkflowStatus.Runnable)
            .Select(x => x.Id).ToListAsync(cancellationToken);
    }

    public async Task<WorkflowInstance> GetWorkflowInstance(string Id, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        return await db.WorkflowInstances.AsNoTracking().FirstAsync(x => x.Id == Id, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids is null) return [];
        var keys = ids.ToArray();
        await using var db = await createContext(cancellationToken);
        return await db.WorkflowInstances.AsNoTracking().Where(x => keys.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(WorkflowStatus? status, string type, DateTime? createdFrom, DateTime? createdTo, int skip, int take)
    {
        await using var db = await createContext(CancellationToken.None);
        var query = db.WorkflowInstances.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrEmpty(type)) query = query.Where(x => x.WorkflowDefinitionId == type);
        if (createdFrom.HasValue)
        {
            var from = createdFrom.Value.ToUniversalTime();
            query = query.Where(x => x.CreateTime >= from);
        }
        if (createdTo.HasValue)
        {
            var to = createdTo.Value.ToUniversalTime();
            query = query.Where(x => x.CreateTime <= to);
        }
        return await query.OrderBy(x => x.CreateTime).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync();
    }

    public async Task ClearWorkflowRuntime(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.EventSubscriptions.Where(x => x.WorkflowId == workflowInstanceId).ExecuteDeleteAsync(cancellationToken);
            await db.ExecutionErrors.Where(x => x.WorkflowId == workflowInstanceId).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task<string> CreateEventSubscription(EventSubscription subscription, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        db.EventSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);
        return subscription.Id;
    }

    public async Task TerminateSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        await db.EventSubscriptions.Where(x => x.Id == eventSubscriptionId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<EventSubscription> GetSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        return (await db.EventSubscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == eventSubscriptionId, cancellationToken))!;
    }

    public async Task<EventSubscription> GetFirstOpenSubscription(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        var time = asOf.ToUniversalTime();
        return (await db.EventSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventName == eventName && x.EventKey == eventKey
                && x.SubscribeAsOf <= time && x.ExternalToken == null, cancellationToken))!;
    }

    public async Task<bool> SetSubscriptionToken(string eventSubscriptionId, string token, string workerId, DateTime expiry, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        var utcExpiry = expiry.ToUniversalTime();
        return await db.EventSubscriptions.Where(x => x.Id == eventSubscriptionId && x.ExternalToken == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ExternalToken, token)
                .SetProperty(x => x.ExternalTokenExpiry, utcExpiry)
                .SetProperty(x => x.ExternalWorkerId, workerId), cancellationToken) == 1;
    }

    public async Task ClearSubscriptionToken(string eventSubscriptionId, string token, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        await db.EventSubscriptions.Where(x => x.Id == eventSubscriptionId && x.ExternalToken == token)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ExternalToken, (string?)null)
                .SetProperty(x => x.ExternalTokenExpiry, (DateTime?)null)
                .SetProperty(x => x.ExternalWorkerId, (string?)null), cancellationToken);
    }

    public async Task<IEnumerable<EventSubscription>> GetSubscriptions(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        var time = asOf.ToUniversalTime();
        return await db.EventSubscriptions.AsNoTracking()
            .Where(x => x.EventName == eventName && x.EventKey == eventKey && x.SubscribeAsOf <= time)
            .ToListAsync(cancellationToken);
    }

    public async Task<string> CreateEvent(Event newEvent, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        db.Events.Add(newEvent);
        await db.SaveChangesAsync(cancellationToken);
        return newEvent.Id;
    }

    public async Task<Event> GetEvent(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        return await db.Events.AsNoTracking().FirstAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        var time = asAt.ToUniversalTime();
        return await db.Events.AsNoTracking().Where(x => !x.IsProcessed && x.EventTime <= time)
            .Select(x => x.Id).ToListAsync(cancellationToken);
    }

    public async Task MarkEventProcessed(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        await db.Events.Where(x => x.Id == id).ExecuteUpdateAsync(update => update.SetProperty(x => x.IsProcessed, true), cancellationToken);
    }

    public async Task MarkEventUnprocessed(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        await db.Events.Where(x => x.Id == id).ExecuteUpdateAsync(update => update.SetProperty(x => x.IsProcessed, false), cancellationToken);
    }

    public async Task<IEnumerable<string>> GetEvents(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
    {
        await using var db = await createContext(cancellationToken);
        var time = asOf.ToUniversalTime();
        return await db.Events.AsNoTracking()
            .Where(x => x.EventName == eventName && x.EventKey == eventKey && x.EventTime >= time)
            .Select(x => x.Id).ToListAsync(cancellationToken);
    }

    public async Task PersistErrors(IEnumerable<ExecutionError> errors, CancellationToken cancellationToken = default)
    {
        var batch = errors.ToArray();
        if (batch.Length == 0) return;
        await using var db = await createContext(cancellationToken);
        db.ExecutionErrors.AddRange(batch);
        await db.SaveChangesAsync(cancellationToken);
    }

    public bool SupportsScheduledCommands => true;

    public async Task ScheduleCommand(ScheduledCommand command)
    {
        await using var db = await createContext(CancellationToken.None);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ScheduledCommand" ("CommandName", "Data", "ExecuteTime")
            VALUES ({command.CommandName}, {command.Data}, {command.ExecuteTime})
            ON CONFLICT ("CommandName", "Data") DO NOTHING
            """);
    }

    public async Task ProcessCommands(DateTimeOffset asOf, Func<ScheduledCommand, Task> action, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        var ticks = asOf.UtcDateTime.Ticks;
        var commands = await db.ScheduledCommands.AsNoTracking()
            .Where(x => x.ExecuteTime < ticks).OrderBy(x => x.ExecuteTime).ToListAsync(cancellationToken);
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await action(command);
                await db.ScheduledCommands
                    .Where(x => x.CommandName == command.CommandName && x.Data == command.Data)
                    .ExecuteDeleteAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled command {CommandName} failed; it remains pending", command.CommandName);
            }
        }
    }

    public void EnsureStoreExists()
    {
        // Schema ownership belongs to DbMaintenance, not background workers.
    }
}
