using EIMSNext.MongoDb;

using EIMSNext.Flow.Persistence;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Service
{
    public class WfDbContext : MongoDbContextBase, IWfDbContext
    {
        private const string WorkflowInstanceCollectionName = "Wf_WorkflowInstance";
        private const string SubscriptionCollectionName = "Wf_Subscription";
        private const string EventCollectionName = "Wf_Event";
        private const string ExecutionErrorCollectionName = "Wf_ExecutionError";
        private const string ScheduledCommandCollectionName = "Wf_ScheduledCommand";

        #region Variables

        #endregion

        public WfDbContext(IOptions<MongoDbConfiguration> settings) : base(settings)
        {
        }

        #region Properties
        public IMongoCollection<WorkflowInstance> WorkflowInstances => GetCollection<WorkflowInstance>(WorkflowInstanceCollectionName);
        public IMongoCollection<EventSubscription> EventSubscriptions => GetCollection<EventSubscription>(SubscriptionCollectionName);
        public IMongoCollection<Event> Events => GetCollection<Event>(EventCollectionName);
        public IMongoCollection<ExecutionError> ExecutionErrors => GetCollection<ExecutionError>(ExecutionErrorCollectionName);
        public IMongoCollection<ScheduledCommand> ScheduledCommands => GetCollection<ScheduledCommand>(ScheduledCommandCollectionName);

        #endregion

        #region Methods

        #endregion

        #region Helper       

        #endregion
    }
}
