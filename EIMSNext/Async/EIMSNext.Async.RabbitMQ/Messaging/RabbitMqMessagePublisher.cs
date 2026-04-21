using System.Text;
using System.Text.Json;

using EIMSNext.Async.Abstractions.Messaging;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace EIMSNext.Async.RabbitMQ.Messaging
{
    public class RabbitMqMessagePublisher(IConnectionFactory connectionFactory, IMessageRouteResolver routeResolver, ILogger<RabbitMqMessagePublisher> logger) : IMessagePublisher
    {
        private readonly IConnectionFactory _connectionFactory = connectionFactory;
        private readonly IMessageRouteResolver _routeResolver = routeResolver;
        private readonly ILogger<RabbitMqMessagePublisher> _logger = logger;

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class
        {
            ArgumentNullException.ThrowIfNull(message);

            return PublishCoreAsync(message, cancellationToken);
        }

        private async Task PublishCoreAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
            where TMessage : class
        {
            var queueName = _routeResolver.ResolveQueueName(typeof(TMessage));

            try
            {
                // Create a new connection per publish to avoid startup-time dependency on RabbitMQ availability.
                await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
                await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, body: body, cancellationToken: cancellationToken);

                _logger.LogInformation("Published message {MessageType} to queue {QueueName}", typeof(TMessage).FullName, queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message {MessageType} to queue {QueueName}", typeof(TMessage).FullName, queueName);
                throw;
            }
        }
    }
}
