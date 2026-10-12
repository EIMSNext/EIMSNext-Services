using Autofac;

using EIMSNext.ApiClient.Flow;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.Quartz.Jobs;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Notification;
using EIMSNext.Flow.Persistence;
using EIMSNext.Flow.Service;
using EIMSNext.Service;
using EIMSNext.Service.Contracts;
using EIMSNext.Persistence.PostgreSql;

namespace EIMSNext.Async.Host.Extensions
{
    public class AutofacRegisterModule : AutofacRegisterModuleBase
    {
        public AutofacRegisterModule()
            : base(serviceAssemblies: [typeof(CorporateService).Assembly])
        {
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // EventHub 持有 scoped WebPushLog 仓储，不能跨消息共享 DbContext。
            builder.RegisterType<EventHub>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<FlowApiClient>().AsSelf().SingleInstance();

            builder.RegisterOutboxConsumers();
            builder.RegisterType<OutboxDeliveryJob>().AsSelf().InstancePerDependency();
        }
    }
}

