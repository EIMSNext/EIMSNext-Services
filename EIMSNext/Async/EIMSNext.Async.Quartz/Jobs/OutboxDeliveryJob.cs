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

            // Mongo 时期的 FilterDefinition.And(...) + Sort.Ascending(...) + Take 已换成
            // IQueryable 的 Where/OrderBy/Take。查询直接在数据库端翻译为
            // SELECT ... WHERE "Status" = 'Pending' AND "OutAt" <= @now ORDER BY "OutAt" LIMIT 200
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

            // 死信补偿：Failed 且最后尝试时间已超过最长退避窗口的，重置回 Pending 再投一次。
            var deadLetterOldest = now - RetryBackoffMs[^1];
            var failed = await _outboxRepo.Queryable
                .Where(x => x.Status == OutboxStatus.Failed && x.LastAttemptTime <= deadLetterOldest)
                .OrderBy(x => x.LastAttemptTime)
                .Take(100)
                .ToListAsync();
            foreach (var msg in failed)
            {
                await _outboxRepo.UpdateAsync(msg.Id, setters => setters
                    .SetProperty(x => x.Status, OutboxStatus.Pending)
                    .SetProperty(x => x.OutAt, now)
                    .SetProperty(x => x.Attempt, 0)
                    .SetProperty(x => x.Error, string.Empty));
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
