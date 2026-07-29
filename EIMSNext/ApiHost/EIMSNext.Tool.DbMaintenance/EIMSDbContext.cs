using EIMSNext.Auth.Entities;
using EIMSNext.Core.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WorkflowCore.Models;

namespace EIMSNext.Auth.DbMaintenance
{
    public class EIMSDbContext : MongoDbContextBase
    {
        public EIMSDbContext(IOptions<MongoDbConfiguration> settings) : base(settings)
        {
        }

        public IMongoCollection<Client> Clients => GetCollection<Client>();
        public IMongoCollection<User> Users => GetCollection<User>();
        public IMongoCollection<AuditLogin> AuditLogins => GetCollection<AuditLogin>();
        public IMongoCollection<WorkflowInstance> WorkflowInstances => GetCollection<WorkflowInstance>("Wf_WorkflowInstance");
        public IMongoCollection<EventSubscription> WorkflowEventSubscriptions => GetCollection<EventSubscription>("Wf_Subscription");
        public IMongoCollection<Event> WorkflowEvents => GetCollection<Event>("Wf_Event");
        public IMongoCollection<ScheduledCommand> WorkflowScheduledCommands => GetCollection<ScheduledCommand>("Wf_ScheduledCommand");
    }
}
