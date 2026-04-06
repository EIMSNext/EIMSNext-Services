using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EIMSNext.Async.Core.Messaging
{
    public abstract class TaskConsumerBase<T, TArgs> : BackgroundService where T : TaskConsumerBase<T, TArgs> where TArgs : class
    {
        protected IResolver Resolver { get; private set; }
        protected IConnection MQConn { get; private set; }// 由 Host 项目通过 DI 注入，确保非空
        protected ILogger<T> Logger { get; private set; }
        protected string QueueName { get; private set; }

        public void Enqueue(TArgs args)
        {
        }

        protected TaskConsumerBase(IResolver resolver)
        {
            Resolver = resolver;
            MQConn = resolver.Resolve<IConnection>();
            Logger = resolver.Resolve<ILogger<T>>();

            var attr = typeof(T).GetCustomAttribute<QueueAttribute>();
            QueueName = (attr?.QueueName ?? "default").ToLower();

            Logger.LogInformation("Consumer [{Type}] listening to: {Queue}",
                GetType().Name, QueueName);
        }

        protected abstract Task HandleTaskAsync(string taskType, string argsJson, CancellationToken ct);

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
                    var msg = JsonSerializer.Deserialize<TaskMessage>(body)!;
                    await HandleTaskAsync(msg.TaskType, msg.ArgumentsJson, stoppingToken);
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Task processing failed");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: false);
                }
            };

            channel.BasicConsume(QueueName, autoAck: false, consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
