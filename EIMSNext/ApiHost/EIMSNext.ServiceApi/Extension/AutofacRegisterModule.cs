using Autofac;
using EIMSNext.ApiClient.Flow;
using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Core.Repository;
using EIMSNext.Repository;
using EIMSNext.Service;
using EIMSNext.Service.Interface;
using EIMSNext.ServiceApi.Authorization;
using HKH.Mef2.Integration;

namespace EIMSNext.ServiceApi.Extension
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
            builder.RegisterType<EIMSDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterGeneric(typeof(DbRepository<>)).As(typeof(IRepository<>)).SingleInstance();
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<DefaultResolver>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<IdentityContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterAssemblyTypes(typeof(CorporateService).Assembly).AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterAssemblyTypes(typeof(CorporateApiService).Assembly).AsImplementedInterfaces().InstancePerLifetimeScope();

            builder.RegisterType<FlowApiClient>().AsSelf().SingleInstance();
            //builder.RegisterType<WeChatPublicClient>().AsSelf().SingleInstance();
            //builder.RegisterType<SmsClient>().AsSelf().SingleInstance();
            //builder.RegisterType<CiticClient>().AsSelf().SingleInstance();
            //builder.RegisterType<TianyanchaClient>().AsSelf().SingleInstance();
            //builder.RegisterType<TencentCloudClient>().AsSelf().SingleInstance();
        }
    }
}
