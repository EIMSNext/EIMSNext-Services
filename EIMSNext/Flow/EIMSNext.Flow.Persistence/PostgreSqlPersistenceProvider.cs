using System;
using EIMSNext.Common;
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
        // WorkflowCore 的 StartWorkflow 不赋值 Id，PG 也没有默认主键，落库前由 provider 生成。
        workflow.Id = workflow.Id ?? TsidIdGenerator.NewId();
        await using var db = await createContext(cancellationToken);
        db.WorkflowInstances.Add(workflow);
        AddPointers(db, workflow.Id, workflow.ExecutionPointers);
        await db.SaveChangesAsync(cancellationToken);
        return workflow.Id;
    }

    public async Task PersistWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // 实例行与指针行的更新必须在同一事务里，否则引擎在两步之间崩溃会留下
            // 「实例状态前进了、指针还是旧的」的脏状态。
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.WorkflowInstances.Update(workflow);
            await SyncPointers(db, workflow.Id, workflow.ExecutionPointers, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken = default)
    {
        await using var db = await createContext(cancellationToken);
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // 实例行与指针行的更新必须在同一事务里，否则引擎在两步之间崩溃会留下
            // 「实例状态前进了、指针还是旧的」的脏状态。
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.WorkflowInstances.Update(workflow);
            await SyncPointers(db, workflow.Id, workflow.ExecutionPointers, cancellationToken);
            // 等待活动的节点会随实例一起提交订阅；PG 无默认主键，这里同样要补 Id，
            // 否则 AddRange 会抛「主键为空」，整次持久化失败、实例被反复重跑。
            foreach (var subscription in subscriptions ?? [])
            {
                subscription.Id = subscription.Id ?? TsidIdGenerator.NewId();
            }
            db.EventSubscriptions.AddRange(subscriptions ?? []);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
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
        var workflow = await db.WorkflowInstances.AsNoTracking().FirstAsync(x => x.Id == Id, cancellationToken);
        await AttachPointers(db, workflow, cancellationToken);
        return workflow;
    }

    public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids is null) return [];
        var keys = ids.ToArray();
        await using var db = await createContext(cancellationToken);
        var workflows = await db.WorkflowInstances.AsNoTracking().Where(x => keys.Contains(x.Id)).ToListAsync(cancellationToken);
        await AttachPointers(db, workflows, cancellationToken);
        return workflows;
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
        var workflows = await query.OrderBy(x => x.CreateTime).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync();
        await AttachPointers(db, workflows, CancellationToken.None);
        return workflows;
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
        // 同 CreateNewWorkflow：PG 无默认主键，订阅 Id 由 provider 生成。
        // 缺了这一步，等待活动的节点（如审批节点）会因主键为空使整次持久化失败，
        // 实例状态无法前进而被调度器反复重跑。
        subscription.Id = subscription.Id ?? Guid.NewGuid().ToString("N");
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
        // 同上：PG 无默认主键，事件 Id 由 provider 生成。
        newEvent.Id = newEvent.Id ?? TsidIdGenerator.NewId();
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

    /// <summary>
    /// 首次创建：实例与全部指针一次入库（指针 Id 缺失时补一个，行主键必需）。
    /// </summary>
    private static void AddPointers(IWfDbContext db, string workflowId, IEnumerable<ExecutionPointer>? pointers)
    {
        foreach (var pointer in pointers ?? Enumerable.Empty<ExecutionPointer>())
        {
            if (string.IsNullOrEmpty(pointer.Id)) pointer.Id = TsidIdGenerator.NewId();
            db.Context.Entry(pointer).Property("WorkflowId").CurrentValue = workflowId;
            db.ExecutionPointers.Add(pointer);
        }
    }

    /// <summary>
    /// 差量同步指针：新增的插入、消失的删除、仍在的逐列更新（含 jsonb 载荷）。
    /// 引擎每步只前进少量指针，这样比旧实现「整块 jsonb 全量重写」的写放大小得多，
    /// 也让单个指针可被索引、可被单独更新。
    /// <para>
    /// WfDbContext 全局配置了 <c>QueryTrackingBehavior.NoTracking</c>，这里查出的行都是游离实体，
    /// 必须先 <c>Attach</c> 再改，否则对游离条目调用 SetValues/Remove 不产生任何 UPDATE/DELETE，
    /// 指针状态将永远停在插入时的值（表现为流程被调度器反复重跑）。
    /// </para>
    /// </summary>
    private async Task SyncPointers(IWfDbContext db, string workflowId, IEnumerable<ExecutionPointer>? pointers, CancellationToken cancellationToken)
    {
        var incoming = pointers?.ToList() ?? [];
        var existing = await db.ExecutionPointers
            .Where(p => EF.Property<string>(p, "WorkflowId") == workflowId)
            .ToListAsync(cancellationToken);

        foreach (var stale in existing.Where(e => incoming.All(p => p.Id != e.Id)))
        {
            db.ExecutionPointers.Attach(stale);
            db.ExecutionPointers.Remove(stale);
        }

        foreach (var pointer in incoming)
        {
            var tracked = existing.Find(e => e.Id == pointer.Id);
            if (tracked is null)
            {
                if (string.IsNullOrEmpty(pointer.Id)) pointer.Id = TsidIdGenerator.NewId();
                db.Context.Entry(pointer).Property("WorkflowId").CurrentValue = workflowId;
                db.ExecutionPointers.Add(pointer);
            }
            else
            {
                var entry = db.Context.Entry(tracked);
                if (entry.State == EntityState.Detached)
                    db.ExecutionPointers.Attach(tracked);
                db.Context.Entry(tracked).CurrentValues.SetValues(pointer);
            }
        }
    }

    /// <summary>把子表里的指针装回实例（引擎期望读取到完整的指针集合）。</summary>
    private static async Task AttachPointers(IWfDbContext db, WorkflowInstance workflow, CancellationToken cancellationToken)
    {
        var pointers = await db.ExecutionPointers.AsNoTracking()
            .Where(p => EF.Property<string>(p, "WorkflowId") == workflow.Id)
            .ToListAsync(cancellationToken);
        workflow.ExecutionPointers = new ExecutionPointerCollection(pointers);
    }

    private static async Task AttachPointers(IWfDbContext db, IEnumerable<WorkflowInstance> workflows, CancellationToken cancellationToken)
    {
        var list = workflows as IList<WorkflowInstance> ?? workflows.ToList();
        var ids = list.Select(x => x.Id).ToArray();
        if (ids.Length == 0) return;

        var pointersByWorkflow = await db.ExecutionPointers.AsNoTracking()
            .Where(p => ids.Contains(EF.Property<string>(p, "WorkflowId")))
            .GroupBy(p => EF.Property<string>(p, "WorkflowId"))
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

        foreach (var workflow in list)
            workflow.ExecutionPointers = new ExecutionPointerCollection(pointersByWorkflow.GetValueOrDefault(workflow.Id) ?? []);
    }
}
