using EIMSNext.Async.Abstractions.Messaging;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace EIMSNext.Async.RabbitMQ.Messaging
{
    public sealed class RabbitMqOutboxDeliveryPublisher(
        IConnectionFactory connectionFactory,
        ILogger<RabbitMqOutboxDeliveryPublisher> logger) : IOutboxDeliveryPublisher
    {
        public async Task PublishRawAsync(string queueName, byte[] body, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(body);

            await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, body: body, cancellationToken: cancellationToken);
            logger.LogInformation("Outbox raw message published to queue {QueueName}", queueName);
        }
    }
}
