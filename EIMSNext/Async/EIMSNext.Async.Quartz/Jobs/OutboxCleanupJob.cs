using System.Linq.Expressions;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Persistence.PostgreSql.Outbox;
using HKH.Mef2.Integration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace EIMSNext.Async.Quartz.Jobs
{
    /// <summary>
    /// 出箱表保留期清理 Job（Quartz 定时）。
    /// 分批物理删除超期行，避免长事务锁表：Sent 按 SentTime、Failed 按 LastAttemptTime
    /// （Failed 为终态死信）、ProcessedMessage Completed 按 ProcessedTime。
    /// 保留期缺省：Sent 14 天 / Failed 30 天 / Processed 30 天，可配 Outbox:*RetentionDays。
    /// </summary>
    [DisallowConcurrentExecution]
    public class OutboxCleanupJob : JobBase<OutboxCleanupJob>
    {
        private const int BatchSize = 2000;

        private readonly IRepository<OutboxMessage> _outboxRepo;
        private readonly IRepository<ProcessedMessage> _processedRepo;
        private readonly int _sentRetentionDays;
        private readonly int _failedRetentionDays;
        private readonly int _processedRetentionDays;

        public OutboxCleanupJob(IResolver resolver)
            : base(resolver)
        {
            _outboxRepo = resolver.GetRepository<OutboxMessage>();
            _processedRepo = resolver.GetRepository<ProcessedMessage>();
            var config = resolver.Resolve<IConfiguration>();
            _sentRetentionDays = config.GetValue("Outbox:SentRetentionDays", 14);
            _failedRetentionDays = config.GetValue("Outbox:FailedRetentionDays", 30);
            // Completed 去重记录必须至少覆盖 Outbox 的保留期，并额外留出队列积压窗口，
            // 否则延迟消息在去重记录清理后到达会再次执行。
            var configuredProcessedRetentionDays = config.GetValue("Outbox:ProcessedRetentionDays", 30);
            _processedRetentionDays = Math.Max(
                configuredProcessedRetentionDays,
                _sentRetentionDays + 7);
        }

        protected override Task ExecuteAsync(IJobExecutionContext context)
        {
            return ExecuteInternalAsync();
        }

        private async Task ExecuteInternalAsync()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var sent = await DeleteBatchesAsync(_outboxRepo, x =>
                x.Status == OutboxStatus.Sent && x.SentTime != null && x.SentTime <= now - _sentRetentionDays * 86_400_000L);
            var failed = await DeleteBatchesAsync(_outboxRepo, x =>
                x.Status == OutboxStatus.Failed && x.LastAttemptTime != null && x.LastAttemptTime <= now - _failedRetentionDays * 86_400_000L);
            var processed = await DeleteBatchesAsync(_processedRepo, x =>
                x.Status == ProcessedMessageStatus.Completed && x.ProcessedTime != null && x.ProcessedTime <= now - _processedRetentionDays * 86_400_000L);

            Logger.LogInformation("Outbox cleanup done: sent={Sent}, failed={Failed}, processed={Processed}", sent, failed, processed);
        }

        private static async Task<int> DeleteBatchesAsync<T>(IRepository<T> repo, Expression<Func<T, bool>> predicate)
            where T : class, IEntityKey
        {
            var total = 0;
            while (true)
            {
                var ids = await repo.Queryable.Where(predicate).Select(x => x.Id).Take(BatchSize).ToListAsync();
                if (ids.Count == 0)
                {
                    break;
                }
                // 重新带上原筛选条件，避免查询 ID 后消息被投递/重置期间仍按旧 ID 删除。
                // 例如 Sent 在查询后被改回 Pending 时，必须保留下来继续投递。
                total += await repo.DeleteManyAsync(AndAlso(
                    predicate,
                    x => ids.Contains(x.Id)));
                if (ids.Count < BatchSize)
                {
                    break;
                }
            }
            return total;
        }

        private static Expression<Func<T, bool>> AndAlso<T>(
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
            where T : class
        {
            var parameter = Expression.Parameter(typeof(T), "entity");
            var leftBody = new ReplaceParameterVisitor(left.Parameters[0], parameter).Visit(left.Body)!;
            var rightBody = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body)!;
            return Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(leftBody, rightBody),
                parameter);
        }

        private sealed class ReplaceParameterVisitor(
            ParameterExpression source,
            ParameterExpression target) : ExpressionVisitor
        {
            protected override Expression VisitParameter(ParameterExpression node)
                => node == source ? target : base.VisitParameter(node);
        }
    }
}
