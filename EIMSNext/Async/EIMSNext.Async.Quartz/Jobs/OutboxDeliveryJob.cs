using System.Text;

using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Persistence.Outbox;
using HKH.Mef2.Integration;

using Microsoft.Extensions.Logging;
using Quartz;

using MongoDB.Driver;

namespace EIMSNext.Async.Quartz.Jobs
{
    /// <summary>
    /// 出箱扫描投递 Job（Quartz 定时）。
    /// 周期性扫 OutboxMessage 表中到期 Pending 行 → 经底层投递器推送 → 成功标记 Sent / 失败标记 Failed；
    /// 同时对超退避阈值的死信按补偿窗口重置回 Pending，提供“已落库未发出”的自愈能力。
    /// </summary>
    [DisallowConcurrentExecution]
    public class OutboxDeliveryJob : JobBase<OutboxDeliveryJob>
    {
        private static readonly long[] RetryBackoffMs = [1 * 60_000, 5 * 60_000, 30 * 60_000, 2 * 3600_000];
        private static readonly int MaxRetryAttempts = RetryBackoffMs.Length;

        private readonly IOutboxDeliveryPublisher _deliveryPublisher;
        private readonly IRepository<OutboxMessage> _outboxRepo;

        public OutboxDeliveryJob(IResolver resolver)
            : base(resolver)
        {
            _deliveryPublisher = resolver.Resolve<IOutboxDeliveryPublisher>();
            _outboxRepo = resolver.GetRepository<OutboxMessage>();
        }

        protected override Task ExecuteAsync(IJobExecutionContext context)
        {
            return ExecuteInternalAsync();
        }

        private async Task ExecuteInternalAsync()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var ready = _outboxRepo.Find(new MongoFindOptions<OutboxMessage>
            {
                Filter = Builders<OutboxMessage>.Filter.And(
                    Builders<OutboxMessage>.Filter.Eq(x => x.Status, OutboxStatus.Pending),
                    Builders<OutboxMessage>.Filter.Lte(x => x.OutAt, now)),
                Sort = Builders<OutboxMessage>.Sort.Ascending(x => x.OutAt),
                Take = 200
            }).ToList();
            Logger.LogDebug("Outbox delivery scan found {Count} ready messages", ready.Count);
            foreach (var msg in ready)
            {
                await DeliverOnceAsync(msg);
            }

            var deadLetterOldest = now - RetryBackoffMs[^1];
            var failed = _outboxRepo.Find(new MongoFindOptions<OutboxMessage>
            {
                Filter = Builders<OutboxMessage>.Filter.And(
                    Builders<OutboxMessage>.Filter.Eq(x => x.Status, OutboxStatus.Failed),
                    Builders<OutboxMessage>.Filter.Lte(x => x.LastAttemptTime, deadLetterOldest)),
                Sort = Builders<OutboxMessage>.Sort.Ascending(x => x.LastAttemptTime),
                Take = 100
            }).ToList();
            foreach (var msg in failed)
            {
                await _outboxRepo.UpdateAsync(msg.Id, Builders<OutboxMessage>.Update
                    .Set(x => x.Status, OutboxStatus.Pending)
                    .Set(x => x.OutAt, now)
                    .Set(x => x.Attempt, 0)
                    .Set(x => x.Error, string.Empty), upsert: false);
                Logger.LogInformation("Outbox dead-letter {Id} (key={Key}) reset to Pending, attempt={Attempt}",
                    msg.Id, msg.IdempotencyKey, 0);
            }
        }

        private async Task DeliverOnceAsync(OutboxMessage msg)
        {
            try
            {
                await _deliveryPublisher.PublishRawAsync(msg.QueueName, Encoding.UTF8.GetBytes(msg.Payload));
                var sentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await _outboxRepo.UpdateAsync(msg.Id, Builders<OutboxMessage>.Update
                    .Set(x => x.Status, OutboxStatus.Sent)
                    .Set(x => x.SentTime, sentTime)
                    .Set(x => x.SentAt, DateTimeOffset.FromUnixTimeMilliseconds(sentTime).UtcDateTime), upsert: false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Outbox {Id} delivery failed, key={Key}", msg.Id, msg.IdempotencyKey);
                var nextAttempt = msg.Attempt + 1;
                var failedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var retryable = nextAttempt <= MaxRetryAttempts;
                var retryAt = failedAt + RetryBackoffMs[Math.Min(nextAttempt - 1, RetryBackoffMs.Length - 1)];
                await _outboxRepo.UpdateAsync(msg.Id, Builders<OutboxMessage>.Update
                    .Set(x => x.Status, retryable ? OutboxStatus.Pending : OutboxStatus.Failed)
                    .Set(x => x.Attempt, nextAttempt)
                    .Set(x => x.LastAttemptTime, failedAt)
                    .Set(x => x.OutAt, retryAt)
                    .Set(x => x.Error, ex.Message), upsert: false);
            }
        }
    }
}
