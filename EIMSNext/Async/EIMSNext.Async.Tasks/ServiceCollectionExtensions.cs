using EIMSNext.Async.Tasks.Consumers;
using EIMSNext.Async.Tasks.SystemTask;

using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Async.Tasks
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAsyncTaskConsumers(this IServiceCollection services)
        {
            services.AddSingleton<ISystemTaskTokenProvider, SystemTaskTokenProvider>();
            services.AddHostedService<NotifyDispatchConsumer>();
            services.AddHostedService<SystemMessageConsumer>();
            services.AddHostedService<EmailConsumer>();
            services.AddHostedService<DataExportConsumer>();
            services.AddHostedService<DataImportConsumer>();
            services.AddHostedService<WebhookConsumer>();
            services.AddHostedService<DataflowRunConsumer>();
            services.AddHostedService<WorkflowExpireConsumer>();

            return services;
        }
    }
}
