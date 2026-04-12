namespace EIMSNext.Async.Abstractions.Messaging
{
    public interface IMessagePublisher
    {
        Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class;
    }
}
