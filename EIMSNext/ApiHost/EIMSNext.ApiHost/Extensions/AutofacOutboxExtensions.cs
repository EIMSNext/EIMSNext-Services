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
            // OutboxPublisher 持有 scoped IRepository/DbContext，不能注册为单例。
            builder.RegisterType<OutboxPublisher>().As<IOutboxPublisher>().InstancePerLifetimeScope();
        }

        public static void RegisterOutboxConsumers(this ContainerBuilder builder)
        {
            builder.RegisterOutboxPublisher();
            // MessageProcessingRepository 通过 IRepository 访问 DbContext，必须跟随消息 scope。
            builder.RegisterType<MessageProcessingRepository>().As<IMessageProcessingRepository>().InstancePerLifetimeScope();
            builder.RegisterType<RabbitMqOutboxDeliveryPublisher>().As<IOutboxDeliveryPublisher>().SingleInstance();
        }
    }
}

