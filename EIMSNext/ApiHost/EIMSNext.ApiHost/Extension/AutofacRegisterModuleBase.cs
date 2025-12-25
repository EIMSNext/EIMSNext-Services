using Autofac;

using EIMSNext.ApiHost.Authorization;
using EIMSNext.Common;
using EIMSNext.Core.Repository;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiHost.Extension
{
    public abstract class AutofacRegisterModuleBase : Module
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AppSetting>().AsSelf().SingleInstance();
            builder.RegisterGeneric(typeof(DbRepository<>)).As(typeof(IRepository<>)).SingleInstance();
            builder.RegisterType<DefaultResolver>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<IdentityContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}
