using System.Text;
using System.Text.Json;

using EIMSNext.Async.Abstractions.Messaging;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EIMSNext.Async.RabbitMQ.Messaging
{
    public abstract class TaskConsumerBase<TMessage, TConsumer> : BackgroundService
        where TMessage : class
        where TConsumer : class
    {
        protected IResolver Resolver { get; }

        protected IConnection MQConn { get; }

        protected ILogger<TConsumer> Logger { get; }

        protected string QueueName { get; }

        protected TaskConsumerBase(IResolver resolver)
        {
            Resolver = resolver;
            MQConn = resolver.Resolve<IConnection>();
            Logger = resolver.Resolve<ILogger<TConsumer>>();
            var routeResolver = resolver.Resolve<IMessageRouteResolver>();
            QueueName = routeResolver.ResolveQueueName(typeof(TMessage));
        }

        protected abstract Task HandleAsync(TMessage message, CancellationToken cancellationToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var channel = MQConn.CreateModel();
            channel.QueueDeclare(QueueName, durable: true);
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

                    await HandleAsync(message, stoppingToken);
                    channel.BasicAck(ea.DeliveryTag, false);
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
