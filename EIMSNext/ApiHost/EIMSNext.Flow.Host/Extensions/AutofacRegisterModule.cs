using Autofac;

using EIMSNext.ApiClient.File;
using EIMSNext.ApiClient.Flow;
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

            // EF 节点写数复用 UI 提交管线（EfDataProcessor 进程内直调 FormDataService）。
            // 流程表单的 Start 仍通过 FlowApiClient 回本进程的 Start 端点；EF 触发消息
            // 通过当前数据库事务写入 Outbox，由异步消费者调用 Run 端点。
            builder.RegisterType<FlowApiClient>().AsSelf().SingleInstance();

            // Print 节点惰性上传经 FileApiClient 调 File.Host /api/v1/Upload（复用其校验与附件落表）。
            builder.RegisterType<FileApiClient>().As<IFileClient>().SingleInstance();

            // 数据库上下文由 AutofacRegisterModuleBase 统一注册的 PostgreSqlDbContext 提供
            // （与 File.Host 保持一致），无需在此重复注册。
            builder.RegisterOutboxPublisher();
        }
    }
}

