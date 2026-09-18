using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence;

public interface IWfDbContext : IAsyncDisposable
{
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<EventSubscription> EventSubscriptions { get; }
    DbSet<Event> Events { get; }
    DbSet<ExecutionError> ExecutionErrors { get; }
    DbSet<ScheduledCommand> ScheduledCommands { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
