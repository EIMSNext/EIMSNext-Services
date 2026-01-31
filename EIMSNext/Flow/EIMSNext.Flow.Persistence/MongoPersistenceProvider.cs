using MongoDB.Driver;
using MongoDB.Driver.Linq;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Persistence
{
    public class MongoPersistenceProvider : IMongoPersistenceProvider
    {
        internal const string WorkflowCollectionName = "Wf_WorkflowInstance";
        private readonly IMongoDatabase _database;

        public MongoPersistenceProvider(IMongoDatabase database)
        {
            _database = database;
        }

        private IMongoCollection<WorkflowInstance> WorkflowInstances => _database.GetCollection<WorkflowInstance>(WorkflowCollectionName);

        private IMongoCollection<EventSubscription> EventSubscriptions => _database.GetCollection<EventSubscription>("Wf_Subscription");

        private IMongoCollection<Event> Events => _database.GetCollection<Event>("Wf_Event");

        private IMongoCollection<ExecutionError> ExecutionErrors => _database.GetCollection<ExecutionError>("Wf_ExecutionError");

        private IMongoCollection<ScheduledCommand> ScheduledCommands => _database.GetCollection<ScheduledCommand>("Wf_ScheduledCommand");

        public async Task<string> CreateNewWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
        {
            await WorkflowInstances.InsertOneAsync(workflow, cancellationToken: cancellationToken);
            return workflow.Id;
        }

        public async Task PersistWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
        {
            await WorkflowInstances.ReplaceOneAsync(x => x.Id == workflow.Id, workflow, cancellationToken: cancellationToken);
        }

        public async Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken = default)
        {
            if (subscriptions == null || subscriptions.Count < 1)
            {
                await PersistWorkflow(workflow, cancellationToken);
                return;
            }

            using (var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken))
            {
                session.StartTransaction();
                await PersistWorkflow(workflow, cancellationToken);
                await EventSubscriptions.InsertManyAsync(subscriptions, cancellationToken: cancellationToken);
                await session.CommitTransactionAsync(cancellationToken);
            }
        }

        public async Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt, CancellationToken cancellationToken = default)
        {
            var now = asAt.ToUniversalTime().Ticks;
            var query = WorkflowInstances
                .Find(x => x.NextExecution.HasValue && x.NextExecution <= now && x.Status == WorkflowStatus.Runnable)
                .Project(x => x.Id);

            return await query.ToListAsync(cancellationToken);
        }

        public async Task<WorkflowInstance> GetWorkflowInstance(string Id, CancellationToken cancellationToken = default)
        {
            var result = await WorkflowInstances.FindAsync(x => x.Id == Id, cancellationToken: cancellationToken);
            return await result.FirstAsync(cancellationToken);
        }

        public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        {
            if (ids == null)
            {
                return new List<WorkflowInstance>();
            }

            var result = await WorkflowInstances.FindAsync(x => ids.Contains(x.Id), cancellationToken: cancellationToken);
            return await result.ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(WorkflowStatus? status, string type, DateTime? createdFrom, DateTime? createdTo, int skip, int take)
        {
            IQueryable<WorkflowInstance> result = WorkflowInstances.AsQueryable();

            if (status.HasValue)
                result = result.Where(x => x.Status == status.Value);

            if (!string.IsNullOrEmpty(type))
                result = result.Where(x => x.WorkflowDefinitionId == type);

            if (createdFrom.HasValue)
                result = result.Where(x => x.CreateTime >= createdFrom.Value);

            if (createdTo.HasValue)
                result = result.Where(x => x.CreateTime <= createdTo.Value);

            return await result.Skip(skip).Take(take).ToListAsync();
        }

        public IQueryable<WorkflowInstance> GetWorkflowInstancesByReference(IEnumerable<string> references, WorkflowStatus? status)
        {
            IQueryable<WorkflowInstance> result = WorkflowInstances.AsQueryable().Where(x => references.Contains(x.Reference));

            if (status.HasValue)
                result = result.Where(x => x.Status == status.Value);

            return result;
        }
        public IQueryable<WorkflowInstance> GetWorkflowInstancesByDefId(IEnumerable<string> defIds, WorkflowStatus? status)
        {
            IQueryable<WorkflowInstance> result = WorkflowInstances.AsQueryable().Where(x => defIds.Contains(x.WorkflowDefinitionId));

            if (status.HasValue)
                result = result.Where(x => x.Status == status.Value);

            return result;
        }
        public IQueryable<WorkflowInstance> GetWorkflowInstances()
        {
            return WorkflowInstances.AsQueryable();
        }

        public async Task<string> CreateEventSubscription(EventSubscription subscription, CancellationToken cancellationToken = default)
        {
            await EventSubscriptions.InsertOneAsync(subscription, cancellationToken: cancellationToken);
            return subscription.Id;
        }

        public async Task TerminateSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default)
        {
            await EventSubscriptions.DeleteOneAsync(x => x.Id == eventSubscriptionId, cancellationToken);
        }

        public async Task<EventSubscription> GetSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default)
        {
            var result = await EventSubscriptions.FindAsync(x => x.Id == eventSubscriptionId, cancellationToken: cancellationToken);
            return await result.FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<EventSubscription> GetFirstOpenSubscription(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
        {
            var query = EventSubscriptions
                .Find(x => x.EventName == eventName && x.EventKey == eventKey && x.SubscribeAsOf <= asOf && x.ExternalToken == null);

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> SetSubscriptionToken(string eventSubscriptionId, string token, string workerId, DateTime expiry, CancellationToken cancellationToken = default)
        {
            var update = Builders<EventSubscription>.Update
                .Set(x => x.ExternalToken, token)
                .Set(x => x.ExternalTokenExpiry, expiry)
                .Set(x => x.ExternalWorkerId, workerId);

            var result = await EventSubscriptions.UpdateOneAsync(x => x.Id == eventSubscriptionId && x.ExternalToken == null, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task ClearSubscriptionToken(string eventSubscriptionId, string token, CancellationToken cancellationToken = default)
        {
            var update = Builders<EventSubscription>.Update
                .Set(x => x.ExternalToken, null)
                .Set(x => x.ExternalTokenExpiry, null)
                .Set(x => x.ExternalWorkerId, null);

            await EventSubscriptions.UpdateOneAsync(x => x.Id == eventSubscriptionId && x.ExternalToken == token, update, cancellationToken: cancellationToken);
        }

        public async Task<IEnumerable<EventSubscription>> GetSubscriptions(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
        {
            var query = EventSubscriptions
                .Find(x => x.EventName == eventName && x.EventKey == eventKey && x.SubscribeAsOf <= asOf);

            return await query.ToListAsync(cancellationToken);
        }

        public async Task<string> CreateEvent(Event newEvent, CancellationToken cancellationToken = default)
        {
            await Events.InsertOneAsync(newEvent, cancellationToken: cancellationToken);
            return newEvent.Id;
        }

        public async Task<Event> GetEvent(string id, CancellationToken cancellationToken = default)
        {
            var result = await Events.FindAsync(x => x.Id == id, cancellationToken: cancellationToken);
            return await result.FirstAsync(cancellationToken);
        }

        public async Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken cancellationToken = default)
        {
            var now = asAt.ToUniversalTime();
            var query = Events
                .Find(x => !x.IsProcessed && x.EventTime <= now)
                .Project(x => x.Id);

            return await query.ToListAsync(cancellationToken);
        }

        public async Task MarkEventProcessed(string id, CancellationToken cancellationToken = default)
        {
            var update = Builders<Event>.Update
                .Set(x => x.IsProcessed, true);

            await Events.UpdateOneAsync(x => x.Id == id, update, cancellationToken: cancellationToken);
        }

        public async Task<IEnumerable<string>> GetEvents(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
        {
            var query = Events
                .Find(x => x.EventName == eventName && x.EventKey == eventKey && x.EventTime >= asOf)
                .Project(x => x.Id);

            return await query.ToListAsync(cancellationToken);
        }

        public async Task MarkEventUnprocessed(string id, CancellationToken cancellationToken = default)
        {
            var update = Builders<Event>.Update
                .Set(x => x.IsProcessed, false);

            await Events.UpdateOneAsync(x => x.Id == id, update, cancellationToken: cancellationToken);
        }

        public async Task PersistErrors(IEnumerable<ExecutionError> errors, CancellationToken cancellationToken = default)
        {
            if (errors.Any())
                await ExecutionErrors.InsertManyAsync(errors, cancellationToken: cancellationToken);
        }

        public bool SupportsScheduledCommands => true;

        public async Task ScheduleCommand(ScheduledCommand command)
        {
            try
            {
                await ScheduledCommands.InsertOneAsync(command);
            }
            catch (MongoWriteException ex)
            {
                if (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                    return;
                throw;
            }
            catch (MongoBulkWriteException ex)
            {
                if (ex.WriteErrors.All(x => x.Category == ServerErrorCategory.DuplicateKey))
                    return;
                throw;
            }
        }

        public async Task ProcessCommands(DateTimeOffset asOf, Func<ScheduledCommand, Task> action, CancellationToken cancellationToken = default)
        {
            var cursor = await ScheduledCommands.FindAsync(x => x.ExecuteTime < asOf.UtcDateTime.Ticks);
            while (await cursor.MoveNextAsync(cancellationToken))
            {
                foreach (var command in cursor.Current)
                {
                    try
                    {
                        await action(command);
                        await ScheduledCommands.DeleteOneAsync(x => x.CommandName == command.CommandName && x.Data == command.Data);
                    }
                    catch (Exception)
                    {
                        //TODO: add logger
                    }
                }
            }
        }

        public void EnsureStoreExists()
        {

        }
    }
}
