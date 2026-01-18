using Autofac;

using EIMSNext.ApiHost.Extension;
using EIMSNext.FileUpload;
using EIMSNext.Service.Interface;

namespace EIMSNext.FileUploadApi.Extension
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
