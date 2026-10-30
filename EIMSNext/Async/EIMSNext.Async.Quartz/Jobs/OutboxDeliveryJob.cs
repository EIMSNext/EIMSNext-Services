using System.Text;

using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Persistence.PostgreSql.Outbox;
using HKH.Mef2.Integration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace EIMSNext.Async.Quartz.Jobs
{
    /// <summary>
    /// 出箱扫描投递 Job（Quartz 定时）。
    /// 周期性扫 OutboxMessage 表中到期 Pending 行 → 经底层投递器推送 → 成功标记 Sent / 失败标记 Failed；
    /// 超过退避次数的消息保留为终态死信，等待排查，不会被自动重置。
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

            // IQueryable 的 Where/OrderBy/Take。查询直接在数据库端翻译为
            // SELECT ... WHERE "Status" = 0 AND "OutAt" <= @now ORDER BY "OutAt" LIMIT 200
            var ready = await _outboxRepo.Queryable
                .Where(x => x.Status == OutboxStatus.Pending && x.OutAt <= now)
                .OrderBy(x => x.OutAt)
                .Take(200)
                .ToListAsync();
            Logger.LogDebug("Outbox delivery scan found {Count} ready messages", ready.Count);
            foreach (var msg in ready)
            {
                await DeliverOnceAsync(msg);
            }
        }

        private async Task DeliverOnceAsync(OutboxMessage msg)
        {
            try
            {
                await _deliveryPublisher.PublishRawAsync(msg.QueueName, Encoding.UTF8.GetBytes(msg.Payload));
                var sentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await _outboxRepo.UpdateAsync(msg.Id, setters => setters
                    .SetProperty(x => x.Status, OutboxStatus.Sent)
                    .SetProperty(x => x.SentTime, sentTime)
                    .SetProperty(x => x.SentAt, DateTimeOffset.FromUnixTimeMilliseconds(sentTime).UtcDateTime));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Outbox {Id} delivery failed, key={Key}", msg.Id, msg.IdempotencyKey);
                var nextAttempt = msg.Attempt + 1;
                var failedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var retryable = nextAttempt <= MaxRetryAttempts;
                var retryAt = failedAt + RetryBackoffMs[Math.Min(nextAttempt - 1, RetryBackoffMs.Length - 1)];
                await _outboxRepo.UpdateAsync(msg.Id, setters => setters
                    .SetProperty(x => x.Status, retryable ? OutboxStatus.Pending : OutboxStatus.Failed)
                    .SetProperty(x => x.Attempt, nextAttempt)
                    .SetProperty(x => x.LastAttemptTime, failedAt)
                    .SetProperty(x => x.OutAt, retryAt)
                    .SetProperty(x => x.Error, ex.Message));
            }
        }
    }
}
