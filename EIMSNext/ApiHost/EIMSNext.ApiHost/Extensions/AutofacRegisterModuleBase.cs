using System.Reflection;
using Autofac;

using EIMSNext.ApiHost.Authorization;
using EIMSNext.Common;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Services;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiHost.Extensions
{
    public abstract class AutofacRegisterModuleBase : Autofac.Module
    {
        private readonly Assembly[] _serviceAssemblies;
        private readonly Assembly[] _apiServiceAssemblies;

        protected AutofacRegisterModuleBase(
            Assembly[]? serviceAssemblies = null,
            Assembly[]? apiServiceAssemblies = null)
        {
            _serviceAssemblies = serviceAssemblies ?? [];
            _apiServiceAssemblies = apiServiceAssemblies ?? [];
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AppSetting>().AsSelf().SingleInstance();
            builder.RegisterGeneric(typeof(DbRepository<>)).As(typeof(IRepository<>)).SingleInstance();
            builder.RegisterType<AggregateService>().AsSelf().SingleInstance();
            builder.RegisterType<DefaultResolver>().AsImplementedInterfaces().InstancePerLifetimeScope();
            builder.RegisterType<IdentityContext>().AsImplementedInterfaces().InstancePerLifetimeScope();

            foreach (var asm in _serviceAssemblies)
            {
                builder.RegisterAssemblyTypes(asm)
                       .AsImplementedInterfaces().InstancePerLifetimeScope();
            }
            foreach (var asm in _apiServiceAssemblies)
            {
                builder.RegisterAssemblyTypes(asm)
                       .AsSelf().AsImplementedInterfaces().InstancePerLifetimeScope();
            }
        }
    }
}
