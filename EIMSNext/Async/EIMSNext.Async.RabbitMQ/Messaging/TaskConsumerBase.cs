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
        private readonly IServiceScopeFactory _scopeFactory;
        protected IConnectionFactory MQConnFactory { get; }

        protected ILogger<TConsumer> Logger { get; }

        protected string QueueName { get; }

        protected TaskConsumerBase(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;

            using var scope = _scopeFactory.CreateScope();
            var serviceProvider = scope.ServiceProvider;
            MQConnFactory = serviceProvider.GetRequiredService<IConnectionFactory>();
            Logger = serviceProvider.GetRequiredService<ILogger<TConsumer>>();
            var routeResolver = serviceProvider.GetRequiredService<IMessageRouteResolver>();
            QueueName = routeResolver.ResolveQueueName(typeof(TMessage));
        }

        protected abstract Task HandleAsync(TMessage message, CancellationToken cancellationToken, IResolver resolver);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var connection = await MQConnFactory.CreateConnectionAsync(stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
            await channel.BasicQosAsync(0, 1, false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var message = JsonSerializer.Deserialize<TMessage>(body);

                    if (message == null)
                    {
                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        return;
                    }

                    await ExecuteInScopeAsync(message, stoppingToken);
                    await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Task processing failed for queue {QueueName}", QueueName);
                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, cancellationToken: stoppingToken);
                }
            };

            await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        protected internal virtual async Task ExecuteInScopeAsync(TMessage message, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IResolver>();
            await HandleAsync(message, cancellationToken, resolver);
        }
    }
}
