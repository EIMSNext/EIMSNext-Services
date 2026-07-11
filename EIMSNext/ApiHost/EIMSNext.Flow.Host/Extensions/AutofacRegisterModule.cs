using Autofac;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Persistence;
using EIMSNext.Flow.Service;
using EIMSNext.Service;
using EIMSNext.Service.Contracts;

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
        }
    }
}
