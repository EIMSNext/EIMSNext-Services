using Autofac;

using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.RabbitMQ.Outbox;

namespace EIMSNext.ApiHost.Extensions
{
    public static class AutofacOutboxExtensions
    {
        public static void RegisterOutboxPublisher(this ContainerBuilder builder)
        {
            builder.RegisterType<OutboxIdempotencyKeyFactory>().As<IOutboxIdempotencyKeyFactory>().SingleInstance();
            builder.RegisterType<OutboxPublisher>().As<IOutboxPublisher>().SingleInstance();
        }

        public static void RegisterOutboxConsumers(this ContainerBuilder builder)
        {
            builder.RegisterOutboxPublisher();
            builder.RegisterType<MessageProcessingRepository>().As<IMessageProcessingRepository>().SingleInstance();
            builder.RegisterType<RabbitMqOutboxDeliveryPublisher>().As<IOutboxDeliveryPublisher>().SingleInstance();
        }
    }
}
