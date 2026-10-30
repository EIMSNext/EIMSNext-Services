namespace EIMSNext.Async.Abstractions.Messaging
{
    public interface IOutboxPublisher
    {
        Task EnqueueAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class, IOutboxMessage;

        Task EnqueueAsync<TMessage>(string idempotencyKey, TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class, IOutboxMessage;

    }

    /// <summary>
    /// 事务内写入 Outbox 的严格接口。写入失败必须抛出，交由外层业务事务回滚。
    /// 幂等键由 <see cref="IOutboxIdempotencyKeyFactory"/> 统一生成，键规则不散落到调用方。
    /// </summary>
    public interface ITransactionalOutboxPublisher
    {
        Task EnqueueRequiredAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class, IOutboxMessage;
    }

    public interface IOutboxDeliveryPublisher
    {
        Task PublishRawAsync(string queueName, byte[] body, CancellationToken cancellationToken = default);
    }

    public interface IOutboxIdempotencyKeyFactory
    {
        string Create<TMessage>(TMessage message) where TMessage : class, IOutboxMessage;
    }

    public interface IMessageProcessingRepository
    {
        Task<string?> TryAcquireAsync(string eventKey, string target, DateTime leaseUntil, CancellationToken cancellationToken = default);

        Task<bool> IsCompletedAsync(string eventKey, string target, CancellationToken cancellationToken = default);

        Task<bool> MarkCompletedAsync(string eventKey, string target, string leaseToken, long processedTime, CancellationToken cancellationToken = default);

        /// <summary>
        /// 释放当前消费者持有的租约。令牌必须匹配，避免误释放其他消费者的租约。
        /// </summary>
        Task<bool> ReleaseAsync(string eventKey, string target, string leaseToken, CancellationToken cancellationToken = default);
    }
}
