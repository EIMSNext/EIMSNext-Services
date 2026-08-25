using Autofac;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.Async.RabbitMQ.Outbox;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Persistence;
using EIMSNext.Flow.Service;
using EIMSNext.Service;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Persistence;

namespace EIMSNext.Flow.Host.Extensions
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

            builder.RegisterType<WfDbContext>().As<IWfDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();

            builder.RegisterType<EIMSDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterOutboxPublisher();
        }
    }
}
