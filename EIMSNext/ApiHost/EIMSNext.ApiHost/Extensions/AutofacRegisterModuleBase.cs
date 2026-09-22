using System.Reflection;
using Autofac;

using EIMSNext.ApiHost.Authorization;
using EIMSNext.Common;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services;
using EIMSNext.Persistence.PostgreSql;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

            // 仓储实现已切换到 PostgreSQL：DbRepository<T> 直接消费 EF Core 的 DbContext。
            // 旧 Mongo 实现（MongoRepository）不再注册，避免同一接口出现两个实现导致解析歧义。
            builder.RegisterGeneric(typeof(DbRepository<>)).As(typeof(IRepository<>)).InstancePerLifetimeScope();
            builder.Register(c =>
            {
                var settings = c.Resolve<IOptions<PostgreSqlOptions>>().Value;
                var options = new DbContextOptionsBuilder<PostgreSqlDbContext>();
                PostgreSqlPersistenceRegistration.ConfigurePostgreSql(options, settings);
                return options.Options;
            }).As<DbContextOptions<PostgreSqlDbContext>>().InstancePerLifetimeScope();
            builder.RegisterType<PostgreSqlDbContext>().AsSelf().InstancePerLifetimeScope();

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
