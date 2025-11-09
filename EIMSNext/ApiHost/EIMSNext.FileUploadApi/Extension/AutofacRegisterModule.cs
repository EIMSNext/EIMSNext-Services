using Autofac;
using EIMSNext.Common;
using EIMSNext.Core.Repository;
using EIMSNext.FileUpload;
using EIMSNext.FileUploadApi.Authorization;
using HKH.Mef2.Integration;

namespace EIMSNext.FileUploadApi.Extension
{
    /// <summary>
    /// 
    /// </summary>
    public class AutofacRegisterModule : Module
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AppSetting>().AsSelf().SingleInstance();
            builder.RegisterType<UploadDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterGeneric(typeof(DbRepository<>)).As(typeof(IRepository<>)).SingleInstance();
            builder.RegisterType<UploadServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<DefaultResolver>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<IdentityContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}
