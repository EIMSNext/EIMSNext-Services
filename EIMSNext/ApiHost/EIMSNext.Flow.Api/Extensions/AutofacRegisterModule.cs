using Autofac;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Service;
using EIMSNext.Service;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Flow.Api.Extensions
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

            builder.RegisterType<WfDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterAssemblyTypes(typeof(CorporateService).Assembly).AsImplementedInterfaces().InstancePerLifetimeScope();
            //builder.RegisterAssemblyTypes(typeof(CorporateApiService).Assembly).AsImplementedInterfaces().InstancePerLifetimeScope();    //builder.RegisterAssemblyTypes(typeof(SysUserService).Assembly).AsSelf().AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}
