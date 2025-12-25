using Autofac;

using EIMSNext.ApiHost.Extension;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Service;
using EIMSNext.Service;

namespace EIMSNext.FlowApi.Extension
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
            builder.RegisterType<WfServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterAssemblyTypes(typeof(CorporateService).Assembly).AsImplementedInterfaces().InstancePerLifetimeScope();
            //builder.RegisterAssemblyTypes(typeof(CorporateApiService).Assembly).AsImplementedInterfaces().InstancePerLifetimeScope();    //builder.RegisterAssemblyTypes(typeof(SysUserService).Assembly).AsSelf().AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}
