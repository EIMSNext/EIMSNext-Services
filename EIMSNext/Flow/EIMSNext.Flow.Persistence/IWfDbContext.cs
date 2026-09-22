using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence;

/// <summary>
/// 工作流持久化上下文抽象。
/// </summary>
/// <remarks>
/// <para>
/// 这里保留 <see cref="Database"/> 与 <see cref="SaveChangesAsync"/> 等成员，是为了让使用方
/// 不必依赖具体的 <c>WfDbContext</c> 类型。迁移到 PostgreSQL 后新增了 <see cref="Context"/>：
/// </para>
/// <para>
/// <b>为什么需要 <see cref="Context"/></b>：<c>TransactionScope</c> 与
/// <c>TransactionScope.ExecuteWithRetryAsync</c> 的入参是 <see cref="DbContext"/>。
/// 原 Mongo 时期把 <c>IClientSessionHandle</c> 一路透传，现在改由 <see cref="DbContext"/>
/// 承载事务，因此接口必须能给出这个上下文对象。实现类本就是 <see cref="DbContext"/> 子类
/// （<c>WfDbContext : DbContext, IWfDbContext</c>），所以这里只是一个类型收窄。
/// </para>
/// </remarks>
public interface IWfDbContext : IAsyncDisposable
{
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<ExecutionPointer> ExecutionPointers { get; }
    DbSet<EventSubscription> EventSubscriptions { get; }
    DbSet<Event> Events { get; }
    DbSet<ExecutionError> ExecutionErrors { get; }
    DbSet<ScheduledCommand> ScheduledCommands { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取底层 EF Core 上下文，用于建立事务作用域。
    /// </summary>
    DbContext Context { get; }
}
