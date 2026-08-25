using System.Text.Json;

using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Service.Persistence.Outbox;

using Microsoft.Extensions.Logging;

namespace EIMSNext.Async.RabbitMQ.Outbox
{
    public sealed class OutboxPublisher(
        IRepository<OutboxMessage> repository,
        IOutboxIdempotencyKeyFactory keyFactory,
        ILogger<OutboxPublisher> logger) : IOutboxPublisher
    {
        public Task EnqueueAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class, IOutboxMessage
        {
            ArgumentNullException.ThrowIfNull(message);
            return EnqueueAsync(keyFactory.Create(message), message, cancellationToken);
        }

        public async Task EnqueueAsync<TMessage>(string idempotencyKey, TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class, IOutboxMessage
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
                ArgumentNullException.ThrowIfNull(message);
                var queueName = typeof(TMessage).GetCustomAttributes(typeof(OutboxQueueAttribute), false)
                    .OfType<OutboxQueueAttribute>()
                    .FirstOrDefault()?.QueueName
                    ?? throw new InvalidOperationException($"{typeof(TMessage).Name} has no OutboxQueueAttribute.");

                await repository.InsertAsync(new OutboxMessage
                {
                    Id = repository.NewId(),
                    IdempotencyKey = idempotencyKey,
                    QueueName = queueName,
                    MessageType = typeof(TMessage).FullName ?? typeof(TMessage).Name,
                    Payload = JsonSerializer.Serialize(message),
                    Status = OutboxStatus.Pending,
                    OutAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
            catch (Exception ex)
            {
                // Product decision: external delivery failures must not roll back committed business data.
                logger.LogError(ex, "Outbox enqueue failed for {MessageType}, key={IdempotencyKey}", typeof(TMessage).FullName, idempotencyKey);
            }
        }
    }
}
