using Autofac;
using EIMSNext.ApiClient.Flow;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.CloudEvent;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Persistence;

namespace EIMSNext.Service.Api.Extensions
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

            builder.RegisterType<EIMSDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterAssemblyTypes(typeof(CorporateService).Assembly).AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterAssemblyTypes(typeof(CorporateApiService).Assembly).AsSelf().AsImplementedInterfaces().InstancePerLifetimeScope();

            builder.RegisterType<FlowApiClient>().AsSelf().SingleInstance();
            //builder.RegisterType<WeChatPublicClient>().AsSelf().SingleInstance();
            //builder.RegisterType<SmsClient>().AsSelf().SingleInstance();
            //builder.RegisterType<CiticClient>().AsSelf().SingleInstance();
            //builder.RegisterType<TianyanchaClient>().AsSelf().SingleInstance();
            //builder.RegisterType<TencentCloudClient>().AsSelf().SingleInstance();

            //TODO:注入测试，将来移到异步项目
            builder.RegisterType<EventHub>().AsImplementedInterfaces().SingleInstance();
        }
    }
}
