using Autofac;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.Async.RabbitMQ.Outbox;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Persistence;
using EIMSNext.Flow.Service;
using EIMSNext.Service;
using EIMSNext.Service.Contracts;
using EIMSNext.Persistence.PostgreSql;

namespace EIMSNext.Flow.Host.Extensions
{
    public class AutofacRegisterModule : AutofacRegisterModuleBase
    {
        public AutofacRegisterModule()
            : base(serviceAssemblies: [typeof(CorporateService).Assembly])
        {
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<ServiceContext>().AsImplementedInterfaces().InstancePerLifetimeScope();

            // 迁移说明：原来这里注册 Mongo 时期的 EIMSDbContext。PostgreSQL 迁移后
            // 数据库上下文由 AutofacRegisterModuleBase 统一注册的 PostgreSqlDbContext 提供
            // （与 File.Host 保持一致），无需在此重复注册。
            builder.RegisterOutboxPublisher();
        }
    }
}

