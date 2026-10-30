using System.Text.Json;

using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Core.Repositories;
using EIMSNext.Persistence.PostgreSql.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Async.RabbitMQ.Outbox
{
    public sealed class OutboxPublisher(
        IRepository<OutboxMessage> repository,
        IOutboxIdempotencyKeyFactory keyFactory,
        ILogger<OutboxPublisher> logger) : IOutboxPublisher, ITransactionalOutboxPublisher
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
                await EnqueueCoreAsync(idempotencyKey, message, cancellationToken);
            }
            catch (Exception ex)
            {
                // Product decision: external delivery failures must not roll back committed business data.
                logger.LogError(ex, "Outbox enqueue failed for {MessageType}, key={IdempotencyKey}", typeof(TMessage).FullName, idempotencyKey);
            }
        }

        public Task EnqueueRequiredAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class, IOutboxMessage
            => EnqueueCoreAsync(keyFactory.Create(message), message, cancellationToken);

        private async Task EnqueueCoreAsync<TMessage>(string idempotencyKey, TMessage message, CancellationToken cancellationToken)
            where TMessage : class, IOutboxMessage
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
            ArgumentNullException.ThrowIfNull(message);
            var queueName = typeof(TMessage).GetCustomAttributes(typeof(OutboxQueueAttribute), false)
                .OfType<OutboxQueueAttribute>()
                .FirstOrDefault()?.QueueName
                ?? throw new InvalidOperationException($"{typeof(TMessage).Name} has no OutboxQueueAttribute.");

            // 快速跳过已存在键；并发竞争由 INSERT ... ON CONFLICT DO NOTHING 原子兜底，
            // 不能靠捕获唯一冲突，因为 PostgreSQL 会把当前事务标记为失败。
            if (await repository.Queryable.AnyAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken))
            {
                logger.LogDebug("Outbox message with key {IdempotencyKey} already enqueued, skipped.", idempotencyKey);
                return;
            }

            var outboxId = repository.NewId();
            var payload = JsonSerializer.Serialize(message);
            var messageType = typeof(TMessage).FullName ?? typeof(TMessage).Name;
            var outAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await repository.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "OutboxMessage"
                    ("Id", "IdempotencyKey", "QueueName", "MessageType", "Payload", "Status", "OutAt")
                VALUES
                    ({outboxId}, {idempotencyKey}, {queueName}, {messageType}, {payload}, {(int)OutboxStatus.Pending}, {outAt})
                ON CONFLICT ("IdempotencyKey") DO NOTHING
                """, cancellationToken);
        }
    }
}
