using Autofac;
using EIMSNext.Common;
using EIMSNext.Core.Repository;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Service;
using EIMSNext.FlowApi.Authorization;
using EIMSNext.Service;
using HKH.Mef2.Integration;

namespace EIMSNext.FlowApi.Extension
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
            builder.RegisterType<WfDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterGeneric(typeof(DbRepository<>)).As(typeof(IRepository<>)).SingleInstance();
            builder.RegisterType<WfServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<DefaultResolver>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<IdentityContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterAssemblyTypes(typeof(CorporateService).Assembly).AsImplementedInterfaces().InstancePerLifetimeScope();
            //builder.RegisterAssemblyTypes(typeof(CorporateApiService).Assembly).AsImplementedInterfaces().InstancePerLifetimeScope();    //builder.RegisterAssemblyTypes(typeof(SysUserService).Assembly).AsSelf().AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}
