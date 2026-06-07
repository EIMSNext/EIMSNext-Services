using Autofac;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.File;
using EIMSNext.Service.Contracts;

namespace EIMSNext.File.Host.Extensions
{
    public class AutofacRegisterModule : AutofacRegisterModuleBase
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<UploadDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
    }
}
}
