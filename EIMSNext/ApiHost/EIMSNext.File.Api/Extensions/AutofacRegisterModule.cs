using Autofac;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.FileUpload;
using EIMSNext.Service.Contracts;

namespace EIMSNext.File.Api.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public class AutofacRegisterModule : AutofacRegisterModuleBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<UploadDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}
