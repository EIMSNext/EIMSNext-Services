using Autofac;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.File;
using EIMSNext.Service;
using EIMSNext.Service.Contracts;

namespace EIMSNext.File.Host.Extensions
{
    public class AutofacRegisterModule : AutofacRegisterModuleBase
    {
        public AutofacRegisterModule()
            : base(serviceAssemblies: [typeof(UploadedFileService).Assembly])
        {
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // File.Host 的仓储由 PostgreSqlDbContext 统一提供，无需单独注册上下文。
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}

