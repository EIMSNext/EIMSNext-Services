using EIMSNext.Core.Mongo;
using MongoDB.Driver;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence
{
    public interface IWfDbContext : IMongoDbContex
    {
        IMongoCollection<WorkflowInstance> WorkflowInstances { get; }
        IMongoCollection<EventSubscription> EventSubscriptions { get; }
        IMongoCollection<Event> Events { get; }
        IMongoCollection<ExecutionError> ExecutionErrors { get; }
        IMongoCollection<ScheduledCommand> ScheduledCommands { get; }
    }
}
