namespace EIMSNext.Async.Abstractions.Messaging
{
    public interface IOutboxPublisher
    {
        Task EnqueueAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class, IOutboxMessage;

        Task EnqueueAsync<TMessage>(string idempotencyKey, TMessage message, CancellationToken cancellationToken = default)
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

        Task<bool> MarkCompletedAsync(string eventKey, string target, string leaseToken, long processedTime, CancellationToken cancellationToken = default);
    }
}
