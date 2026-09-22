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

            // 迁移说明：UploadDbContext 已随 Mongo 基础设施一并移除；
            // File.Host 的仓储由 PostgreSqlDbContext 统一提供，无需单独注册上下文。
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}

