using Autofac;

using EIMSNext.ApiClient.Flow;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.Quartz.Jobs;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.CloudEvent;
using EIMSNext.Flow.Persistence;
using EIMSNext.Flow.Service;
using EIMSNext.Service;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Persistence;

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

            builder.RegisterType<EIMSDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<WfDbContext>().As<IWfDbContext>().SingleInstance();
            builder.RegisterType<EventHub>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<FlowApiClient>().AsSelf().SingleInstance();

            builder.RegisterOutboxConsumers();
            builder.RegisterType<OutboxDeliveryJob>().AsSelf().InstancePerDependency();
        }
    }
}
