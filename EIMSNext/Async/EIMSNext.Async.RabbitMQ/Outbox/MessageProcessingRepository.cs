using System.Data;

using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common;
using EIMSNext.Core.Repositories;
using EIMSNext.Persistence.PostgreSql.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Npgsql;

namespace EIMSNext.Async.RabbitMQ.Outbox
{
    /// <summary>
    /// 消费幂等仓储。用租约（lease）+ 唯一键实现「同一 eventKey + target 只允许一个消费者处理」。
    /// </summary>
    public sealed class MessageProcessingRepository(IRepository<ProcessedMessage> repository) : IMessageProcessingRepository
    {
        /// <summary>
        /// 尝试获取处理租约。
        /// </summary>
        /// <param name="leaseUntil">租约到期时间。</param>
        /// <returns>租约令牌；已被他人持有或已完成时为 null。</returns>
        /// <remarks>
        /// <code language="sql">
        /// insert into "ProcessedMessage" (...) values (...)
        /// on conflict ("EventKey", "Target") do update
        ///     set "Status" = 'Processing', "LeaseUntil" = ..., "LeaseToken" = ...
        ///     where P."Status" &lt;&gt; 'Completed'
        ///       and (P."LeaseUntil" is null or P."LeaseUntil" &lt;= now())
        /// returning "LeaseToken"
        /// </code>
        /// <para>
        /// <b>为什么是更好的方案：</b>
        /// <list type="bullet">
        /// <item><description><b>原子性天然成立</b>：<c>insert ... on conflict do update</c> 在 PostgreSQL 里
        /// 会先对该唯一索引加行级排他锁，两个并发消费者不可能同时拿到令牌，无需再依赖捕获异常。</description></item>
        /// <item><description><b>失败可判定</b>：<c>do update ... where ...</c> 的 <c>where</c> 不满足时不更新，
        /// <c>returning</c> 不返回任何行，与「没抢到」一一对应，不再需要 <c>result != null</c> 的隐式判定。</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        public async Task<string?> TryAcquireAsync(string eventKey, string target, DateTime leaseUntil, CancellationToken cancellationToken = default)
        {
            var token = TsidIdGenerator.NewId();

            const string sql = """
                insert into "ProcessedMessage"
                    ("Id", "EventKey", "Target", "Status", "LeaseUntil", "LeaseToken", "ProcessedTime", "ProcessedAt")
                values
                    (@id, @eventKey, @target, @status, @leaseUntil, @token, null, null)
                on conflict ("EventKey", "Target") do update
                    set "Status" = @status,
                        "LeaseUntil" = @leaseUntil,
                        "LeaseToken" = @token,
                        "ProcessedTime" = null,
                        "ProcessedAt" = null
                    where "ProcessedMessage"."Status" <> @completed
                      and ("ProcessedMessage"."LeaseUntil" is null
                           or "ProcessedMessage"."LeaseUntil" <= @now)
                returning "LeaseToken"
                """;

            var acquired = await ExecuteScalarAsync(
                sql,
                command =>
                {
                    command.Parameters.AddWithValue("id", repository.NewId());
                    command.Parameters.AddWithValue("eventKey", eventKey);
                    command.Parameters.AddWithValue("target", target);
                    command.Parameters.AddWithValue("status", ProcessedMessageStatus.Processing);
                    command.Parameters.AddWithValue("completed", ProcessedMessageStatus.Completed);
                    command.Parameters.AddWithValue("leaseUntil", leaseUntil);
                    command.Parameters.AddWithValue("token", token);
                    command.Parameters.AddWithValue("now", DateTime.UtcNow);
                },
                cancellationToken);

            return acquired is null or DBNull ? null : token;
        }

        /// <summary>
        /// 标记处理完成，并释放租约。
        /// </summary>
        /// <param name="leaseToken">租约令牌，必须与当前持有者一致。</param>
        /// <param name="processedTime">处理时间（Unix 毫秒）。</param>
        /// <returns>成功接管并标记时为 true。</returns>
        /// <remarks>
        /// <c>where "LeaseToken" = @token</c> 是租约续约的核心约束：如果租约已过期并被其他消费者抢走，
        /// 持有者不能覆盖别人的状态。
        /// </remarks>
        public async Task<bool> MarkCompletedAsync(string eventKey, string target, string leaseToken, long processedTime, CancellationToken cancellationToken = default)
        {
            var affected = await repository.UpdateManyAsync(
                x => x.EventKey == eventKey
                     && x.Target == target
                     && x.Status == ProcessedMessageStatus.Processing
                     && x.LeaseToken == leaseToken,
                setters => setters
                    .SetProperty(x => x.Status, ProcessedMessageStatus.Completed)
                    .SetProperty(x => x.ProcessedTime, processedTime)
                    .SetProperty(x => x.ProcessedAt, DateTimeOffset.FromUnixTimeMilliseconds(processedTime).UtcDateTime)
                    .SetProperty(x => x.LeaseUntil, (DateTime?)null)
                    .SetProperty(x => x.LeaseToken, (string?)null),
                cancellationToken);

            return affected == 1;
        }

        /// <summary>
        /// 释放失败处理的租约，使 RabbitMQ 重投后可以立即重新获取。
        /// </summary>
        public async Task<bool> ReleaseAsync(string eventKey, string target, string leaseToken, CancellationToken cancellationToken = default)
        {
            var affected = await repository.UpdateManyAsync(
                x => x.EventKey == eventKey
                     && x.Target == target
                     && x.Status == ProcessedMessageStatus.Processing
                     && x.LeaseToken == leaseToken,
                setters => setters
                    .SetProperty(x => x.LeaseUntil, (DateTime?)null)
                    .SetProperty(x => x.LeaseToken, (string?)null),
                cancellationToken);

            return affected == 1;
        }

        /// <summary>
        /// 在 DbContext 的底层连接上执行单值原生 SQL。
        /// </summary>
        /// <returns>首行首列的值；无结果时为 null。</returns>
        /// <remarks>
        /// <c>insert ... on conflict ... returning</c> 无法用 EF Core 的 <c>ExecuteUpdate</c> 表达
        /// （后者只支持 UPDATE/DELETE，返回行数而非结果集），因此这里直接走 ADO.NET。
        /// 连接从仓储的 <see cref="IRepository{T}.DbContext"/> 取，保证与同一作用域内的
        /// EF 操作共用连接与事务。
        /// </remarks>
        private async Task<object?> ExecuteScalarAsync(
            string sql,
            Action<NpgsqlCommand> bind,
            CancellationToken cancellationToken)
        {
            var connection = repository.DbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                if (repository.DbContext.Database.CurrentTransaction is { } transaction)
                {
                    command.Transaction = transaction.GetDbTransaction();
                }

                bind((NpgsqlCommand)command);
                return await command.ExecuteScalarAsync(cancellationToken);
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}
