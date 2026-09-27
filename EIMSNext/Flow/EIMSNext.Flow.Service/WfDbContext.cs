using EIMSNext.Flow.Persistence;
using Microsoft.EntityFrameworkCore;
using WorkflowCore.Models;
namespace EIMSNext.Flow.Service;
public sealed class WfDbContext(DbContextOptions<WfDbContext> options) : DbContext(options), IWfDbContext
{
 public DbSet<WorkflowInstance> WorkflowInstances=>Set<WorkflowInstance>(); public DbSet<ExecutionPointer> ExecutionPointers=>Set<ExecutionPointer>(); public DbSet<EventSubscription> EventSubscriptions=>Set<EventSubscription>(); public DbSet<Event> Events=>Set<Event>(); public DbSet<ExecutionError> ExecutionErrors=>Set<ExecutionError>(); public DbSet<ScheduledCommand> ScheduledCommands=>Set<ScheduledCommand>();

 /// <inheritdoc />
 DbContext IWfDbContext.Context => this;

 protected override void OnModelCreating(ModelBuilder builder) => builder.ConfigureWorkflowStore();
}
