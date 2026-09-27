using Autofac;
using EIMSNext.ApiClient.Flow;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.Async.RabbitMQ.Outbox;
using EIMSNext.Service;
using EIMSNext.Service.Contracts;
using EIMSNext.Persistence.Mongo;
using EIMSNext.Plugin;

namespace EIMSNext.Service.Host.Extensions
{
    public class AutofacRegisterModule : AutofacRegisterModuleBase
    {
        public AutofacRegisterModule()
            : base(
                serviceAssemblies: [typeof(CorporateService).Assembly],
                apiServiceAssemblies: [typeof(CorporateApiService).Assembly])
        {
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<EIMSDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<FlowApiClient>().AsSelf().SingleInstance();
            builder.RegisterAssemblyTypes(typeof(PluginInstallService).Assembly)
                .AsImplementedInterfaces()
                .InstancePerLifetimeScope();

            builder.RegisterOutboxPublisher();
        }
    }
}
