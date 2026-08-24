using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Service.Persistence.Outbox;

using MongoDB.Driver;

namespace EIMSNext.Async.RabbitMQ.Outbox
{
    public sealed class MessageProcessingRepository(IRepository<ProcessedMessage> repository) : IMessageProcessingRepository
    {
        public async Task<string?> TryAcquireAsync(string eventKey, string target, DateTime leaseUntil, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var token = Guid.NewGuid().ToString("N");
            var filter = Builders<ProcessedMessage>.Filter.And(
                Builders<ProcessedMessage>.Filter.Eq(x => x.EventKey, eventKey),
                Builders<ProcessedMessage>.Filter.Eq(x => x.Target, target),
                Builders<ProcessedMessage>.Filter.Ne(x => x.Status, ProcessedMessageStatus.Completed),
                Builders<ProcessedMessage>.Filter.Or(
                    Builders<ProcessedMessage>.Filter.Exists(x => x.LeaseUntil, false),
                    Builders<ProcessedMessage>.Filter.Lte(x => x.LeaseUntil, now)));
            var update = Builders<ProcessedMessage>.Update
                .Set(x => x.Status, ProcessedMessageStatus.Processing)
                .Set(x => x.LeaseUntil, leaseUntil)
                .Set(x => x.LeaseToken, token)
                .Unset(x => x.ProcessedAt)
                .Unset(x => x.ProcessedTime);
            var result = await repository.Collection.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<ProcessedMessage> { IsUpsert = false, ReturnDocument = ReturnDocument.After }, cancellationToken);
            if (result != null)
            {
                return token;
            }

            try
            {
                await repository.InsertAsync(new ProcessedMessage
                {
                    Id = repository.NewId(),
                    EventKey = eventKey,
                    Target = target,
                    Status = ProcessedMessageStatus.Processing,
                    LeaseUntil = leaseUntil,
                    LeaseToken = token
                });
                return token;
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Code is 11000 or 11001 or 11010)
            {
                return null;
            }
        }

        public async Task<bool> MarkCompletedAsync(string eventKey, string target, string leaseToken, long processedTime, CancellationToken cancellationToken = default)
        {
            var filter = Builders<ProcessedMessage>.Filter.And(
                Builders<ProcessedMessage>.Filter.Eq(x => x.EventKey, eventKey),
                Builders<ProcessedMessage>.Filter.Eq(x => x.Target, target),
                Builders<ProcessedMessage>.Filter.Eq(x => x.Status, ProcessedMessageStatus.Processing),
                Builders<ProcessedMessage>.Filter.Eq(x => x.LeaseToken, leaseToken));
            var update = Builders<ProcessedMessage>.Update
                .Set(x => x.Status, ProcessedMessageStatus.Completed)
                .Set(x => x.ProcessedTime, processedTime)
                .Set(x => x.ProcessedAt, DateTimeOffset.FromUnixTimeMilliseconds(processedTime).UtcDateTime)
                .Unset(x => x.LeaseUntil)
                .Unset(x => x.LeaseToken);
            return (await repository.UpdateManyAsync(filter, update, upsert: false)).ModifiedCount == 1;
        }
    }
}
