using System.Text;
using System.Text.Json;

using EIMSNext.Async.Abstractions.Messaging;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace EIMSNext.Async.RabbitMQ.Messaging
{
    public class RabbitMqMessagePublisher(IConnection connection, IMessageRouteResolver routeResolver, ILogger<RabbitMqMessagePublisher> logger) : IMessagePublisher
    {
        private readonly IConnection _connection = connection;
        private readonly IMessageRouteResolver _routeResolver = routeResolver;
        private readonly ILogger<RabbitMqMessagePublisher> _logger = logger;

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class
        {
            ArgumentNullException.ThrowIfNull(message);

            var queueName = _routeResolver.ResolveQueueName(typeof(TMessage));

            try
            {
                using var channel = _connection.CreateModel();
                channel.QueueDeclare(queueName, durable: true);

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
                channel.BasicPublish(exchange: string.Empty, routingKey: queueName, basicProperties: null, body: body);

                _logger.LogInformation("Published message {MessageType} to queue {QueueName}", typeof(TMessage).FullName, queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message {MessageType} to queue {QueueName}", typeof(TMessage).FullName, queueName);
                throw;
            }

            return Task.CompletedTask;
        }
    }
}
