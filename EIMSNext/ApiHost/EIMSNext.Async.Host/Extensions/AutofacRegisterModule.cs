using Autofac;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.Common;
using EIMSNext.Core.Repositories;
using EIMSNext.Service;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Persistence;

using HKH.Mef2.Integration;

namespace EIMSNext.Async.Host.Extensions
{
    public class AutofacRegisterModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AppSetting>().AsSelf().SingleInstance();
            builder.RegisterGeneric(typeof(DbRepository<>)).As(typeof(IRepository<>)).SingleInstance();
            builder.RegisterType<DefaultResolver>().AsImplementedInterfaces().InstancePerLifetimeScope();

            builder.RegisterType<EIMSDbContext>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterAssemblyTypes(typeof(CorporateService).Assembly)
                .AsSelf()
                .AsImplementedInterfaces()
                .InstancePerLifetimeScope();
        }
    }
}
