using System.Text;
using System.Text.Json;

using EIMSNext.Async.Abstractions.Messaging;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EIMSNext.Async.RabbitMQ.Messaging
{
    public abstract class TaskConsumerBase<TMessage, TConsumer> : BackgroundService
        where TMessage : class
        where TConsumer : class
    {
        // Resolver is scoped; do not inject into singleton. Use per-message scope.
        protected IServiceProvider ServiceProvider { get; }
        protected IConnectionFactory MQConnFactory { get; }

        protected ILogger<TConsumer> Logger { get; }

        protected string QueueName { get; }

        protected TaskConsumerBase(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            // Resolve non-scoped dependencies from root container when possible
            MQConnFactory = serviceProvider.GetRequiredService<IConnectionFactory>();
            Logger = serviceProvider.GetRequiredService<ILogger<TConsumer>>();
            var routeResolver = serviceProvider.GetRequiredService<IMessageRouteResolver>();
            QueueName = routeResolver.ResolveQueueName(typeof(TMessage));
        }

        protected abstract Task HandleAsync(TMessage message, CancellationToken cancellationToken, IResolver resolver);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var connection = MQConnFactory.CreateConnection();
            using var channel = connection.CreateModel();
            // Ensure queue is non-exclusive by default
            channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            channel.BasicQos(0, 1, false);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (_, ea) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var message = JsonSerializer.Deserialize<TMessage>(body);

                    if (message == null)
                    {
                        channel.BasicAck(ea.DeliveryTag, false);
                        return;
                    }

                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await HandleAsync(message, stoppingToken, scope.ServiceProvider.GetRequiredService<IResolver>());
                        channel.BasicAck(ea.DeliveryTag, false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Task processing failed for queue {QueueName}", QueueName);
                    channel.BasicNack(ea.DeliveryTag, false, requeue: false);
                }
            };

            channel.BasicConsume(QueueName, autoAck: false, consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
